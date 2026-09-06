using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class StudentModelTests
    {
        [Fact]
        public async Task Student_CanBeSavedWithoutAnAssignedDriver()
        {
            using var context = TestHelpers.BuildContext();

            context.Students.Add(new Student { FullName = "Lucas Silva", ParentId = 1, SchoolId = 1, DriverId = null });
            await context.SaveChangesAsync();

            var saved = Assert.Single(context.Students);
            Assert.Null(saved.DriverId);
            Assert.Equal(1, saved.ParentId);
        }
    }
}
