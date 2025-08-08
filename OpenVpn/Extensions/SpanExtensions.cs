namespace OpenVpn
{
    internal static class SpanExtensions
    {
        public static void MoveRight<T>(this Span<T> self, int offset)
        {
            for (var i = self.Length - offset; i > 0; i -= offset)
            {
                var copySize = Math.Min(i, offset);
                var copyFrom = i - offset;
                var copyTo = i;

                if (copyFrom < 0)
                {
                    copyTo = copyTo + -copyFrom;
                    copyFrom = 0;
                }

                self.Slice(copyFrom, copySize)
                    .CopyTo(self.Slice(copyTo, copySize));
            }

            self.Slice(0, offset).Fill(default!);
        }

        public static void MoveLeft<T>(this Span<T> self, int offset)
        {
            for (var i = 0; i < self.Length - offset; i += offset)
            {
                var copySize = Math.Min(self.Length - i - offset, offset);
                var copyFrom = i + offset;
                var copyTo = i;

                self.Slice(copyFrom, copySize)
                    .CopyTo(self.Slice(copyTo, copySize));
            }

            self.Slice(self.Length - offset, offset).Fill(default!);
        }
    }
}
