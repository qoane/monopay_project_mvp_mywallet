using MonoPayAggregator.Models;
using System.Linq;

namespace MonoPayAggregator.Data
{
    public static class SeedData
    {
        public const string MyWalletTestEmail = "mywallet.tester@example.com";
        public const string MyWalletTestPassword = "MyWalletTest123!";
        public const string MyWalletTestMerchantId = "mywallet-demo-merchant";

        public static void EnsureSeeded(MonoPayDbContext dbContext)
        {
            var existing = dbContext.Users.FirstOrDefault(u => u.Email == MyWalletTestEmail);
            if (existing != null)
            {
                return;
            }

            var seededUser = new User
            {
                FirstName = "MyWallet",
                LastName = "Tester",
                Email = MyWalletTestEmail,
                Phone = "+26650123456",
                PasswordHash = PasswordHelper.HashPassword(MyWalletTestPassword),
                IsVerified = true,
                MerchantId = MyWalletTestMerchantId,
                IsAdmin = false
            };

            dbContext.Users.Add(seededUser);
            dbContext.SaveChanges();
        }
    }
}
