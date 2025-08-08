using OpenVpn.Sessions.Packets;

namespace OpenVpn.Tests
{
    public class SessionPacketHeaderTests
    {
        [Theory]
        [InlineData((byte)0x00, (byte)0x00, (byte)0x00)]
        [InlineData((byte)0x01, (byte)0x02, (byte)0x0A)]
        [InlineData((byte)0x0F, (byte)0x07, (byte)0x7F)]
        [InlineData((byte)0x05, (byte)0x03, (byte)0x2B)]
        public void CombineOpcodeKeyId_ValidInputs_ReturnsCorrectCombination(byte opcode, byte keyId, byte expected)
        {
            // Act
            var result = SessionPacketHeader.CombineOpcodeKeyId(opcode, keyId);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData((byte)0x00, (byte)0x00, (byte)0x00)]
        [InlineData((byte)0x0A, (byte)0x01, (byte)0x02)]
        [InlineData((byte)0x7F, (byte)0x0F, (byte)0x07)]
        [InlineData((byte)0x2B, (byte)0x05, (byte)0x03)]
        public void SplitOpcodeKeyId_ValidInputs_ReturnsCorrectSplit(byte combined, byte expectedOpcode, byte expectedKeyId)
        {
            // Act
            var (opcode, keyId) = SessionPacketHeader.SplitOpcodeKeyId(combined);

            // Assert
            Assert.Equal(expectedOpcode, opcode);
            Assert.Equal(expectedKeyId, keyId);
        }

        [Theory]
        [InlineData((byte)32)] // opcode > 31
        [InlineData((byte)255)] // max byte value
        public void CombineOpcodeKeyId_InvalidOpcode_ShouldThrow(byte invalidOpcode)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SessionPacketHeader.CombineOpcodeKeyId(invalidOpcode, 0));
        }

        [Theory]
        [InlineData((byte)8)] // keyId > 7
        [InlineData((byte)255)] // max byte value
        public void CombineOpcodeKeyId_InvalidKeyId_ShouldThrow(byte invalidKeyId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SessionPacketHeader.CombineOpcodeKeyId(0, invalidKeyId));
        }
    }
}