namespace OpenVpn.Sessions.Packets
{
    internal sealed class SessionPacket : ICloneable
    {
        public required ISessionPacketHeader Header { get; init; }
        public required ReadOnlyMemory<byte> Data { get; init; }

        public SessionPacket Clone()
        {
            return new SessionPacket()
            {
                Header = Header,
                Data = Data.ToArray()
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
