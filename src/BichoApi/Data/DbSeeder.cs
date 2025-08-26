public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.PayoutTables.Any())
        {
            db.PayoutTables.AddRange(new[]{
                new PayoutTable{ Modality="MILHAR",      Key="BASE", MultiplierBp=400000 },
                new PayoutTable{ Modality="MILHAR_INV",  Key="BASE", MultiplierBp=400000 },
                new PayoutTable{ Modality="CENTENA",     Key="BASE", MultiplierBp=60000  },
                new PayoutTable{ Modality="CENTENA_INV", Key="BASE", MultiplierBp=60000  },
                new PayoutTable{ Modality="CENTENA_3X",  Key="3X",   MultiplierBp=60000  },
                new PayoutTable{ Modality="DEZENA",      Key="BASE", MultiplierBp=10000  },
                new PayoutTable{ Modality="GRUPO",       Key="BASE", MultiplierBp=18000  },
                // TODO: DUQUE/TERNO/QUADRA/QUINA/SENA e variações com suas chaves
            });
        }

        if (!db.Users.Any())
        {
            var u = new User { Name = "Admin", Email = "admin@example.com" };
            db.Users.Add(u); db.SaveChanges();
            db.Wallets.Add(new Wallet { UserId = u.Id, BalanceCents = 0 });
        }

        db.SaveChanges();
    }
}