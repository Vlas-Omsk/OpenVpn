using System.Collections.Immutable;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OpenVpn.Configuration;
using OpenVpn.Control;
using OpenVpn.Control.Crypto;
using OpenVpn.Control.Crypto.Tls;
using OpenVpn.Control.Packets;
using OpenVpn.Crypto;
using OpenVpn.Data;
using OpenVpn.Data.Crypto;
using OpenVpn.Data.Packets;
using OpenVpn.Exceptions;
using OpenVpn.IO;
using OpenVpn.Options;
using OpenVpn.Protocol.Packets;
using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using OpenVpn.TlsCrypt;
using Org.BouncyCastle.Security;
using PacketDotNet;
using PacketDotNet.Utils;
using PinkSystem;
using PinkSystem.Net.Sockets;

namespace OpenVpn.Protocol
{
    public sealed class OpenVpnClientProtocol : IOpenVpnProtocol
    {
        private static readonly SecureRandom _random = new();
        private readonly OpenVpnClientConfiguration _configuration;
        private readonly ISocketsProvider _socketsProvider;
        private readonly ILoggerFactory _loggerFactory;
        private ISocket? _socket;
        private Stream? _stream;
        private ISessionChannel? _sessionChannel;
        private SessionChannelDemuxer? _sessionChannelDemuxer;
        private IControlChannel? _controlChannel;
        private IControlCrypto? _controlCrypto;
        private IDataChannel? _dataChannel;
        private CryptoKeySource? _clientKeySource;
        private CryptoKeySource? _serverKeySource;
        private KeyExchangeOptions? _keyExchangeOptions;
        private PushOptions? _pushOptions;
        private readonly MemoryStream _sendStreamBuffer = new();
        private readonly byte[] _waitBuffer = new byte[1];

        public OpenVpnClientProtocol(
            OpenVpnClientConfiguration configuration,
            ISocketsProvider socketsProvider,
            ILoggerFactory loggerFactory
        )
        {
            _configuration = configuration;
            _socketsProvider = socketsProvider;
            _loggerFactory = loggerFactory;
        }

        private KeyExchangeOptions KeyExchangeOptions => _keyExchangeOptions ??
            throw new InvalidOperationException("Push options not received");
        private PushOptions PushOptions => _pushOptions ??
            throw new InvalidOperationException("Push options not received");

        public async Task Connect(CancellationToken cancellationToken)
        {
            await EstablishSessionChannel(cancellationToken);

            EstablishControlChannel();
        }

        public void Write(IOpenVpnProtocolPacket packet)
        {
            switch (packet)
            {
                case EthernetDataPacket ethernetDataPacket:
                    try
                    {
                        ethernetDataPacket.Packet.WriteTo(_sendStreamBuffer);

                        _dataChannel!.Write(new RawDataPacket()
                        {
                            Data = _sendStreamBuffer.ToReadOnlyMemory()
                        });
                    }
                    finally
                    {
                        _sendStreamBuffer.SetLength(0);
                    }
                    break;
                case IpDataPacket ipDataPacket:
                    try
                    {
                        ipDataPacket.Packet.WriteTo(_sendStreamBuffer);

                        _dataChannel!.Write(new RawDataPacket()
                        {
                            Data = _sendStreamBuffer.ToReadOnlyMemory()
                        });
                    }
                    finally
                    {
                        _sendStreamBuffer.SetLength(0);
                    }
                    break;
                default:
                    throw new NotSupportedException("Protocol packet not supported");
            }
        }

        public IOpenVpnProtocolPacket? Read()
        {
            if (_controlChannel != null)
            {
                var packet = _controlChannel.Read();

                switch (packet)
                {
                    case KeyExchangeMethod1Packet:
                        throw new NotImplementedException();
                    case KeyExchangeMethod2Packet keyExchangeMethod2Packet:
                        HandleKeyExchangeMethod2Packet(keyExchangeMethod2Packet);
                        break;
                    case PushReplyPacket pushReplyPacket:
                        return HandlePushReplyPacket(pushReplyPacket);
                    case AuthFailedPacket authFailedPacket:
                        throw new OpenVpnAuthFailedException(authFailedPacket.Reason);
                    case null:
                        break;
                    default:
                        throw new OpenVpnUnexpectedPacketTypeException(packet.GetType());
                }
            }

            if (_dataChannel != null)
            {
                var packet = _dataChannel.Read();

                switch (packet)
                {
                    case PingPacket pingPacket:
                        HandlePingPacket(pingPacket);
                        break;
                    case RawDataPacket rawDataPacket:
                        return HandleRawDataPacket(rawDataPacket);
                    case null:
                        break;
                    default:
                        throw new OpenVpnUnexpectedPacketTypeException(packet.GetType());
                }
            }

            return null;
        }

        private void HandleKeyExchangeMethod2Packet(KeyExchangeMethod2Packet packet)
        {
            _serverKeySource = packet.KeySource.Clone();

            var serializer = new OptionsSerializer();

            var options = serializer.Serialize<KeyExchangeOptions>(packet.Options);

            if (serializer.UnknownOptions.Count > 0)
                throw new OpenVpnProtocolException($"Received unknown options: {string.Join(", ", serializer.UnknownOptions.Keys)}");

            _keyExchangeOptions = options;
        }

        private ConnectPacket HandlePushReplyPacket(PushReplyPacket packet)
        {
            var serializer = new OptionsSerializer();

            _pushOptions = serializer.Serialize<PushOptions>(packet.Options);

            EstablishDataChannel();

            var ifConfigIpv4 = (InterfaceConfig?)null;
            var ifConfigIpv6 = (InterfaceConfig?)null;

            if (_pushOptions.IfConfig != null)
                ifConfigIpv4 = InterfaceConfig.ParseIpv4(_pushOptions.IfConfig, _pushOptions.RouteGateway);

            if (_pushOptions.IfConfigIpv6 != null)
                ifConfigIpv6 = InterfaceConfig.ParseIpv6(_pushOptions.IfConfigIpv6);

            return new ConnectPacket()
            {
                DeviceType = KeyExchangeOptions.DeviceType,
                InterfaceConfigIpv4 = ifConfigIpv4,
                InterfaceConfigIpv6 = ifConfigIpv6,
            };
        }

        private void HandlePingPacket(PingPacket packet)
        {
            _dataChannel!.Write(new PingPacket());
        }

        private IOpenVpnProtocolPacket HandleRawDataPacket(RawDataPacket packet)
        {
            switch (KeyExchangeOptions.DeviceType)
            {
                case OpenVpnDeviceType.Tun:
                    var ipPacket = new RawIPPacket(
                        new ByteArraySegment(packet.Data.ToArray())
                    );

                    return new IpDataPacket()
                    {
                        Packet = (IPPacket)ipPacket.PayloadPacket
                    };
                case OpenVpnDeviceType.Tap:
                    var ethernetPacket = new EthernetPacket(
                        new ByteArraySegment(packet.Data.ToArray())
                    );
                    return new EthernetDataPacket()
                    {
                        Packet = ethernetPacket
                    };
                default:
                    throw new NotSupportedException("Device type not supported");
            }
        }

        public async Task Send(CancellationToken cancellationToken)
        {
            if (_sessionChannel != null)
                await _sessionChannel.Send(cancellationToken);

            if (_controlChannel != null)
                await _controlChannel.Send(cancellationToken);

            if (_dataChannel != null)
                await _dataChannel.Send(cancellationToken);
        }

        public async Task Receive(CancellationToken cancellationToken)
        {
            if (_sessionChannel != null)
                await _sessionChannel.Receive(cancellationToken);

            if (_controlChannel != null)
                await _controlChannel.Receive(cancellationToken);

            if (_dataChannel != null)
                await _dataChannel.Receive(cancellationToken);
        }

        public async Task WaitForData(CancellationToken cancellationToken)
        {
            var readedLength = await _socket!.ReceiveAsync(_waitBuffer, SocketFlags.Peek, cancellationToken);

            if (readedLength == 0)
                throw new OpenVpnConnectionClosedException();
        }

        private async Task EstablishSessionChannel(CancellationToken cancellationToken)
        {
            var socketType = _configuration.Protocol switch
            {
                System.Net.Sockets.ProtocolType.Udp => SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Tcp => SocketType.Stream,
                _ => throw new NotSupportedException("Protocol type")
            };

            _socket = await _socketsProvider.Create(socketType, _configuration.Protocol, cancellationToken);

            await _socket.ConnectAsync(_configuration.Remote, cancellationToken);

            _stream = new CrossplatformNetworkStream(_socket, ownsSocket: true);

            _sessionChannel = new SessionChannel(
                _stream
            );
            _sessionChannelDemuxer = new SessionChannelDemuxer(
                _sessionChannel
            );
        }

        private void EstablishControlChannel()
        {
            var protocolFlags =
                IvProtocollFlags.DataV2 |
                IvProtocollFlags.RequestPush |
                IvProtocollFlags.AuthPending |
                IvProtocollFlags.DnsOption |
                IvProtocollFlags.ExitNotify |
                IvProtocollFlags.AuthFailTemp |
                IvProtocollFlags.DynamicTlsCrypt;

            switch (_configuration.ControlCrypto)
            {
                case OpenVpnTlsControlCrypto tlsControlCrypto:
                    _controlCrypto = new TlsCrypto(
                        tlsControlCrypto.Certificate,
                        tlsControlCrypto.PrivateKey,
                        _random
                    );

                    if (tlsControlCrypto.UseKeyMaterialExporters)
                        protocolFlags |= IvProtocollFlags.TlsKeyMaterialExport;
                    break;
                case null:
                    _controlCrypto = new Control.Crypto.PlainCrypto();
                    break;
                default:
                    throw new NotSupportedException("Control channel crypto not supported");
            }

            var controlChannel = _sessionChannelDemuxer!.RegisterFor([
                typeof(ControlHardResetClientV2PacketHeader).GetSessionPacketOpcode(),
                typeof(ControlHardResetServerV2PacketHeader).GetSessionPacketOpcode(),
                typeof(ControlV1PacketHeader).GetSessionPacketOpcode()
            ]);

            switch (_configuration.ControlWrapper)
            {
                case OpenVpnTlsCryptControlWrapper tlsCryptControlCryptoWrapper:
                    controlChannel = new TlsCryptWrapper(
                        maximumQueueSize: 4,
                        controlChannel,
                        new CryptoKeys(tlsCryptControlCryptoWrapper.StaticKey),
                        OpenVpnMode.Client,
                        _random,
                        _loggerFactory
                    );
                    break;
                case null:
                    break;
                default:
                    throw new NotSupportedException("Control channel wrapper not supported");
            }

            _controlChannel = new ControlChannel(
                maximumQueueSize: 4,
                controlChannel,
                OpenVpnMode.Client,
                _controlCrypto,
                _loggerFactory
            );

            _clientKeySource = CryptoKeySource.Generate(_random);

            var peerInfo = new Dictionary<string, IReadOnlyList<string>?>()
            {
                { "IV_VER", [_configuration.Version.ToString()] },
                { "IV_PLAT", [_configuration.Platform.GetPeerInfoName()] },
                { "IV_TCPNL", ["1"] }, // TCP non-linear packet sequencing
                { "IV_MTU", ["1600"] }, // Preferred or maximum MTU
                { "IV_NCP", ["2"] }, // Supports cipher negotiation and GCM
                { "IV_CIPHERS", [string.Join(":", _configuration.DataCiphers)] },
                { "IV_PROTO", [((uint)protocolFlags).ToString()] },
                { "IV_GUI_VER", [_configuration.Name] },
                { "IV_SSO", ["openurl,webauth,crtext"] }
            };

            _controlChannel.Connect();

            _controlChannel.Write(new KeyExchangeMethod2Packet()
            {
                KeySource = _clientKeySource.Value,
                Password = string.Empty,
                Username = string.Empty,
                Options = new Dictionary<string, IReadOnlyList<string>?>()
                {
                    { "default", null }
                },
                PeerInfo = peerInfo
            });
        }

        private void EstablishDataChannel()
        {
            var dataChannel = _sessionChannelDemuxer!.RegisterFor([
                typeof(DataV2PacketHeader).GetSessionPacketOpcode()
            ]);

            CryptoKeys keys;

            if (PushOptions.ProtocolFlags?.Contains("tls-ekm") == true)
            {
                keys = ((TlsCrypto)_controlCrypto!).Keys!.Value;
            }
            else
            {
                keys = CryptoKeys.DeriveFromKeySources(
                    _clientKeySource!.Value,
                    _controlChannel!.SessionId,
                    _serverKeySource!.Value,
                    _controlChannel!.RemoteSessionId
                );
            }

            _clientKeySource!.Value.TryClear();
            _clientKeySource = null;
            _serverKeySource!.Value.TryClear();
            _serverKeySource = null;

            var crypto = DataCrypto.Create(
                PushOptions.Cipher,
                KeyExchangeOptions.Auth,
                keys,
                OpenVpnMode.Client,
                epochFormat: false,
                _random
            );

            if (!PushOptions.PeerId.HasValue)
                throw new OpenVpnProtocolException("Server not sent peer id");

            _dataChannel = new DataChannel(
                PushOptions.PeerId.Value,
                maximumQueueSize: 4,
                crypto,
                dataChannel,
                _loggerFactory
            );
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }
    }
}
