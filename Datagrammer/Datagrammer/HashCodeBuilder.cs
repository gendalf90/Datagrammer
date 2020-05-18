using System;

namespace Datagrammer
{
    internal readonly ref struct HashCodeBuilder
    {
        private readonly int resultHash;

        private HashCodeBuilder(int initialHash)
        {
            resultHash = initialHash;
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
                return new HashCodeBuilder(resultHash * 23 + value);
            }
        }

        public int Build()
        {
            return resultHash;
        }
    }
}
