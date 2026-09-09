using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class SmokeTests
    {
        [Fact]
        public async Task Context_CanSaveAndReadASchool()
        {
            using var context = TestHelpers.BuildContext();
            context.Schools.Add(new School { Name = "PUC-Campinas" });
            await context.SaveChangesAsync();

            Assert.Single(context.Schools);
        }
    }
}
