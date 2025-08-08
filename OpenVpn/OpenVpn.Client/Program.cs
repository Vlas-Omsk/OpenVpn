using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OpenVpn.Configuration;
using OpenVpn.Protocol.Packets;
using PinkSystem.Net.Sockets;

namespace OpenVpn.Protocol
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(x => x.AddConsole());
            var socketsProvider = new SystemNetSocketsProvider();

            var endpoint = File.ReadAllText("endpoint.txt").Split(':');

            var options = new OpenVpnClientConfiguration()
            {
                Remote = new IPEndPoint(IPAddress.Parse(endpoint[0]), int.Parse(endpoint[1])),
                Protocol = ProtocolType.Tcp,
                ControlCrypto = new OpenVpnTlsControlCrypto()
                {
                    Certificate = PemUtils.ParseCertificate(
                        File.ReadAllText("cert.pem")
                    ),
                    PrivateKey = PemUtils.ParsePrivateKey(
                        File.ReadAllText("key.pem")
                    )
                },
                ControlWrapper = new OpenVpnTlsCryptControlWrapper()
                {
                    StaticKey = PemUtils.ParseStaticKey(
                        File.ReadAllText("tls-crypt.pem")
                    )
                }
            };

            var logger = loggerFactory.CreateLogger(typeof(Program).Name);

            using var protocol = new OpenVpnClientProtocol(
                options,
                socketsProvider,
                loggerFactory
            );

            await protocol.Connect(CancellationToken.None);

            while (true)
            {
                await protocol.Receive(CancellationToken.None);

                var packet = protocol.Read();

                switch (packet)
                {
                    case IpDataPacket ipDataPacket:
                        logger.LogInformation(ipDataPacket.Packet.ToString(PacketDotNet.StringOutputType.VerboseColored));
                        break;
                    case EthernetDataPacket ethernetDataPacket:
                        logger.LogInformation(ethernetDataPacket.Packet.ToString(PacketDotNet.StringOutputType.VerboseColored));
                        break;
                    case ConnectPacket setupPacket:
                        var icmpPacket = new PacketDotNet.IcmpV4Packet(new PacketDotNet.Utils.ByteArraySegment(new byte[8]));

                        icmpPacket.TypeCode = PacketDotNet.IcmpV4TypeCode.EchoRequest;
                        icmpPacket.Id = 0x1234;
                        icmpPacket.Sequence = 1;
                        icmpPacket.PayloadData = System.Text.Encoding.ASCII.GetBytes("Hello");

                        icmpPacket.UpdateIcmpChecksum();

                        var ipPacket = new PacketDotNet.IPv4Packet(setupPacket.InterfaceConfigIpv4!.Address, IPAddress.Parse("8.8.8.8"));

                        ipPacket.PayloadPacket = icmpPacket;

                        ipPacket.UpdateIPChecksum();

                        protocol.Write(new IpDataPacket()
                        {
                            Packet = ipPacket
                        });
                        break;
                    case null:
                        break;
                }

                await protocol.Send(CancellationToken.None);

                await protocol.WaitForData(CancellationToken.None);
            }

            //var icmpPacket = new PacketDotNet.IcmpV6Packet(new PacketDotNet.Utils.ByteArraySegment(new byte[8]));

            //icmpPacket.Type = PacketDotNet.IcmpV6Type.EchoRequest;
            //icmpPacket.Code = 0;
            //icmpPacket.PayloadData = System.Text.Encoding.ASCII.GetBytes("Hello");

            //var destinationIp = IPAddress.Parse("fd42:11:2::0:0:1");
            //var ipPacket = new PacketDotNet.IPv6Packet(IfConfigIpv6, destinationIp);

            //ipPacket.PayloadPacket = icmpPacket;
            //ipPacket.NextHeader = PacketDotNet.ProtocolType.IcmpV6;
            //ipPacket.PayloadLength = (ushort)icmpPacket.TotalPacketLength;

            //icmpPacket.UpdateIcmpChecksum();

            //_dataChannel.Write(new RawDataPacket()
            //{
            //    Data = ipPacket.Bytes
            //});
        }
    }
}
