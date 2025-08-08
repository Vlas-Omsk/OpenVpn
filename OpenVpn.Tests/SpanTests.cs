namespace OpenVpn.Tests
{
    public class SpanTests
    {
        [Fact]
        public void MoveRight_MovingAndAddingZeroes()
        {
            var actual = new byte[]
            {
                0x00,
                0x01,
                0x02,
                0x03,
                0x04,
                0x05,
                0x06,
                0x07,
            };

            actual.AsSpan().MoveRight(3);

            var expected = new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x00,
                0x01,
                0x02,
                0x03,
                0x04,
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void MoveLeft_MovingAndAddingZeroes()
        {
            var actual = new byte[]
            {
                0x00,
                0x01,
                0x02,
                0x03,
                0x04,
                0x05,
                0x06,
                0x07,
            };

            actual.AsSpan().MoveLeft(3);

            var expected = new byte[]
            {
                0x03,
                0x04,
                0x05,
                0x06,
                0x07,
                0x00,
                0x00,
                0x00,
            };

            Assert.Equal(expected, actual);
        }
    }
}
