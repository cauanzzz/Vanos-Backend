using Vanos.API.Services;

namespace Vanos.API.Tests
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        public void Hash_ProducesHashThatVerifiesAgainstOriginalPassword()
        {
            var hash = _hasher.Hash("Sup3rSecret!");

            Assert.True(_hasher.Verify("Sup3rSecret!", hash));
        }

        [Fact]
        public void Verify_ReturnsFalseForWrongPassword()
        {
            var hash = _hasher.Hash("Sup3rSecret!");

            Assert.False(_hasher.Verify("WrongPassword", hash));
        }
    }
}
