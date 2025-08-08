using CommunityToolkit.HighPerformance;
using OpenVpn.Buffers;
using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.X509;

namespace OpenVpn.Control.Crypto.Tls
{
    internal sealed class TlsCrypto : IControlCrypto
    {
        private readonly TlsClient _client;
        private readonly TlsClientProtocol _protocol = new();
        private readonly byte[] _sendBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly Pipe _sendPipe = new();
        private readonly byte[] _receiveBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly Pipe _receivePipe = new();

        public TlsCrypto(
            X509Certificate certificate,
            AsymmetricKeyParameter privateKey,
            SecureRandom random
        )
        {
            _client = new(
                certificate,
                privateKey,
                random
            );
        }

        public CryptoKeys? Keys => _client.Keys;

        public void Connect()
        {
            _protocol.Connect(_client);
        }

        public void WriteInput(ReadOnlySpan<byte> data)
        {
            _sendPipe.Write(data);
        }

        public int ReadOutput(Span<byte> data)
        {
            if (_client.HandshakeCompleted)
            {
                while (true)
                {
                    var readedLength = _sendPipe.Read(_sendBuffer);

                    if (readedLength == 0)
                        break;

                    _protocol.WriteApplicationData(_sendBuffer.AsSpan(0, readedLength));
                }
            }

            if (_protocol.GetAvailableOutputBytes() > 0)
            {
                var readedLength = _protocol.ReadOutput(_sendBuffer, 0, data.Length);

                _sendBuffer.AsSpan(0, readedLength).CopyTo(data);

                return readedLength;
            }

            return 0;
        }

        public void WriteOutput(ReadOnlySpan<byte> data)
        {
            for (var i = 0; i < data.Length; i += _receiveBuffer.Length)
            {
                var block = data.Slice(i, Math.Min(_receiveBuffer.Length, data.Length - i));

                block.CopyTo(_receiveBuffer);

                _protocol.OfferInput(_receiveBuffer, 0, block.Length);
            }

            if (_client.HandshakeCompleted)
            {
                while (_protocol.GetAvailableInputBytes() > 0)
                {
                    var readedLength = _protocol.ReadInput(_receiveBuffer, 0, _receiveBuffer.Length);

                    _receivePipe.Write(_receiveBuffer.AsSpan(0, readedLength));
                }
            }
        }

        public int ReadInput(Span<byte> data)
        {
            return _receivePipe.Read(data);
        }

        public void Dispose()
        {
        }
    }
}
