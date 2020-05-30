using System;

namespace Datagrammer
{
    internal readonly ref struct HashCodeBuilder
    {
        private readonly int resultHash;

        private HashCodeBuilder(int resultHash)
        {
            this.resultHash = resultHash;
        }

        public HashCodeBuilder Combine(ReadOnlySpan<byte> bytes)
        {
            unchecked
            {
                if (bytes.IsEmpty)
                {
                    return this;
                }

                var hash = 17;

                for (int i = 0; i < bytes.Length; i++)
                {
                    hash = hash * 31 + bytes[i];
                }

                return Combine(hash);
            }
        }

        public HashCodeBuilder Combine(int value)
        {
            unchecked
            {
                if (value == 0)
                {
                    return this;
                }

                return new HashCodeBuilder(resultHash * 23 + value);
            }
        }

        public int Build()
        {
            return resultHash;
        }
    }
}
