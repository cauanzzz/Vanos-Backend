using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;

namespace Vanos.API.Tests
{
    public static class TestHelpers
    {
        public static AppDbContext BuildContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }
}
