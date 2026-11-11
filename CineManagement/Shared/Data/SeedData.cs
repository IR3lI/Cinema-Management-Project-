using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CineManagement.Models;

namespace CineManagement.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();

        // ── Tự động nâng cấp Schema MariaDB cho Cinemas, OutboxMessages, Movies & Tickets ──
        var ddlStatements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS `cinemas` (
                `CinemaId` int NOT NULL AUTO_INCREMENT,
                `Code` varchar(30) NOT NULL,
                `Name` varchar(150) NOT NULL,
                `Address` varchar(300) NOT NULL,
                `City` varchar(100) NOT NULL,
                `Hotline` varchar(30) NULL,
                `Email` varchar(150) NULL,
                `ImageUrl` varchar(500) NULL,
                `Description` varchar(1000) NULL,
                `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                `CreatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`CinemaId`),
                UNIQUE INDEX `IX_cinemas_Code` (`Code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

            @"CREATE TABLE IF NOT EXISTS `outboxmessages` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Type` varchar(100) NOT NULL,
                `Payload` longtext NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `ProcessedAt` datetime(6) NULL,
                `RetryCount` int NOT NULL DEFAULT 0,
                `MaxRetries` int NOT NULL DEFAULT 3,
                `LastError` varchar(2000) NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_outboxmessages_ProcessedAt_RetryCount` (`ProcessedAt`, `RetryCount`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

            "ALTER TABLE `screeningrooms` ADD COLUMN IF NOT EXISTS `CinemaId` int NULL;",
            "ALTER TABLE `screeningrooms` ADD COLUMN IF NOT EXISTS `AisleRows` varchar(255) NULL;",
            "ALTER TABLE `screeningrooms` ADD COLUMN IF NOT EXISTS `AisleCols` varchar(255) NULL;",
            "ALTER TABLE `movies` ADD COLUMN IF NOT EXISTS `Country` varchar(100) NULL DEFAULT 'Mỹ';",
            "ALTER TABLE `movies` ADD COLUMN IF NOT EXISTS `Format` varchar(50) NULL DEFAULT '2D';",
            "ALTER TABLE `movies` ADD COLUMN IF NOT EXISTS `SubtitleType` varchar(100) NULL DEFAULT 'Phụ đề Việt';",
            "ALTER TABLE `tickets` ADD COLUMN IF NOT EXISTS `IsPrinted` tinyint(1) NOT NULL DEFAULT 0;",
            "ALTER TABLE `tickets` ADD COLUMN IF NOT EXISTS `PrintedAt` datetime(6) NULL;",
            "ALTER TABLE `tickets` ADD COLUMN IF NOT EXISTS `PrintedByUserId` varchar(450) NULL;",
            "ALTER TABLE `tickets` ADD COLUMN IF NOT EXISTS `ThermalReceiptCode` varchar(50) NULL;"
        };

        foreach (var sql in ddlStatements)
        {
            try { await context.Database.ExecuteSqlRawAsync(sql); } catch { }
        }

        // ── Roles ──────────────────────────────────────────────
        string[] roles = { "Admin", "Staff", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Default Admin account ──────────────────────────────
        const string adminEmail = "admin@cinema.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Quản trị viên",
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        // ── Seed Cinema Staff Members (Sprint 2 / Team) ──────────
        var staffSeeds = new[]
        {
            new {
                Email = "staff@cinema.com",
                FullName = "Đặng Hoàng Long",
                Phone = "0912111222",
                Position = "Thu ngân quầy vé",
                Department = "Quầy vé (Box Office)",
                Salary = 8500000m,
                DaysAgo = 180,
                Notes = "Phụ trách quầy vé và thanh toán trực tiếp tại sảnh rạp."
            },
            new {
                Email = "staff2@cinema.com",
                FullName = "Lê Thị Hương",
                Phone = "0988222333",
                Position = "Nhân viên soát vé chính",
                Department = "Soát vé & Phòng chiếu",
                Salary = 8800000m,
                DaysAgo = 120,
                Notes = "Phụ trách kiểm tra mã QR vé và hướng dẫn khán giả vào phòng chiếu."
            },
            new {
                Email = "staff.nguyenan@cinema.com",
                FullName = "Nguyễn Văn An",
                Phone = "0903333444",
                Position = "Trưởng ca vận hành (Shift Supervisor)",
                Department = "Quản lý ca trực",
                Salary = 14500000m,
                DaysAgo = 365,
                Notes = "Điều phối toàn bộ hoạt động của ca trực tại rạp CineStar."
            },
            new {
                Email = "staff.phamduyen@cinema.com",
                FullName = "Phạm Thị Duyên",
                Phone = "0974444555",
                Position = "Trưởng quầy F&B (F&B Lead)",
                Department = "Dịch vụ F&B",
                Salary = 9800000m,
                DaysAgo = 210,
                Notes = "Quản lý tồn kho bắp nước và chất lượng phục vụ combo tại quầy bar."
            },
            new {
                Email = "staff.hoangduc@cinema.com",
                FullName = "Hoàng Minh Đức",
                Phone = "0965555666",
                Position = "Kỹ thuật viên phòng chiếu",
                Department = "Kỹ thuật & Chiếu phim",
                Salary = 11500000m,
                DaysAgo = 250,
                Notes = "Vận hành máy chiếu IMAX, Laser 4K và hệ thống âm thanh Dolby Atmos."
            },
            new {
                Email = "staff.vumai@cinema.com",
                FullName = "Vũ Thị Mai",
                Phone = "0936666777",
                Position = "Chuyên viên chăm sóc khách hàng",
                Department = "Chăm sóc khách hàng",
                Salary = 9000000m,
                DaysAgo = 90,
                Notes = "Giải đáp thắc mắc khách hàng, hỗ trợ thành viên và tiếp nhận phản hồi."
            },
            new {
                Email = "staff.trankhang@cinema.com",
                FullName = "Trần Gia Khang",
                Phone = "0917777888",
                Position = "Nhân viên soát vé ca tối",
                Department = "Soát vé & Phòng chiếu",
                Salary = 8200000m,
                DaysAgo = 60,
                Notes = "Hỗ trợ quét mã QR vào phòng chiếu các suất chiếu cao điểm buổi tối."
            },
            new {
                Email = "staff.bichtram@cinema.com",
                FullName = "Bùi Bích Trâm",
                Phone = "0989999000",
                Position = "Thu ngân F&B & Pha chế",
                Department = "Dịch vụ F&B",
                Salary = 8200000m,
                DaysAgo = 45,
                Notes = "Phục vụ bắp nước, combo và nước ngọt tươi tại quầy sảnh chính."
            }
        };

        foreach (var s in staffSeeds)
        {
            var user = await userManager.FindByEmailAsync(s.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName       = s.Email,
                    Email          = s.Email,
                    FullName       = s.FullName,
                    PhoneNumber    = s.Phone,
                    EmailConfirmed = true,
                    IsActive       = true,
                    CreatedAt      = DateTime.Now.AddDays(-s.DaysAgo)
                };
                var createRes = await userManager.CreateAsync(user, "Staff@123");
                if (createRes.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Staff");
                }
            }
            else
            {
                user.FullName = s.FullName;
                user.PhoneNumber = s.Phone;
                await userManager.UpdateAsync(user);
                if (!await userManager.IsInRoleAsync(user, "Staff"))
                {
                    await userManager.AddToRoleAsync(user, "Staff");
                }
            }

            var staffRecord = await context.Staffs.FirstOrDefaultAsync(st => st.UserId == user.Id);
            if (staffRecord == null)
            {
                context.Staffs.Add(new Staff
                {
                    UserId     = user.Id,
                    Position   = s.Position,
                    Department = s.Department,
                    HireDate   = DateTime.Today.AddDays(-s.DaysAgo),
                    Salary     = s.Salary,
                    Notes      = s.Notes,
                    IsActive   = true
                });
                await context.SaveChangesAsync();
            }
            else
            {
                staffRecord.Position = s.Position;
                staffRecord.Department = s.Department;
                staffRecord.Salary = s.Salary;
                staffRecord.Notes = s.Notes;
                context.Staffs.Update(staffRecord);
                await context.SaveChangesAsync();
            }
        }

        // ── Test Customer account ───────────────────────────────
        const string customerEmail = "customer@cinema.com";
        var customerUser = await userManager.FindByEmailAsync(customerEmail);
        if (customerUser == null)
        {
            customerUser = new ApplicationUser
            {
                UserName    = customerEmail,
                Email       = customerEmail,
                FullName    = "Khách hàng Test",
                EmailConfirmed = true,
                IsActive    = true,
                CreatedAt   = DateTime.Now
            };
            var result = await userManager.CreateAsync(customerUser, "Customer@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(customerUser, "Customer");
        }

        // ── Additional Customer accounts for demo history & reviews ───
        var customerAccounts = new[]
        {
            ("nguyenvana@gmail.com",  "Nguyễn Văn An",     "0912345678"),
            ("tranthib@gmail.com",    "Trần Thị Bình",     "0987654321"),
            ("levanc@gmail.com",      "Lê Văn Cường",      "0901234567"),
            ("phamthid@gmail.com",    "Phạm Thị Duyên",    "0976543210"),
            ("hoange@gmail.com",      "Hoàng Minh Đức",    "0965432109"),
            ("vuthimai@gmail.com",    "Vũ Thị Mai",        "0936666777"),
            ("doquanghuy@gmail.com",  "Đỗ Quang Huy",      "0945555888"),
            ("buithutrang@gmail.com", "Bùi Thu Trang",     "0923333999"),
            ("nguyenphuctien@gmail.com", "Nguyễn Phúc Tiến", "0918888111"),
        };

        var customerUsers = new List<ApplicationUser>();
        foreach (var (email, name, phone) in customerAccounts)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName    = email,
                    Email       = email,
                    FullName    = name,
                    PhoneNumber = phone,
                    EmailConfirmed = true,
                    IsActive    = true,
                    CreatedAt   = DateTime.Now.AddDays(-new Random(email.GetHashCode()).Next(7, 60))
                };
                var result = await userManager.CreateAsync(user, "Customer@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Customer");
            }
            customerUsers.Add(user);
        }

        // ── Seed Genres ────────────────────────────────────────
        if (!context.Genres.Any())
        {
            context.Genres.AddRange(
                new Genre { Name = "Hành động",            Description = "Phim hành động, võ thuật, phiêu lưu" },
                new Genre { Name = "Hài hước",             Description = "Phim hài, kịch tính nhẹ nhàng" },
                new Genre { Name = "Kinh dị",              Description = "Phim kinh dị, rùng rợn, giật gân" },
                new Genre { Name = "Tình cảm",             Description = "Phim tình cảm, lãng mạn, gia đình" },
                new Genre { Name = "Hoạt hình",            Description = "Phim hoạt hình, anime" },
                new Genre { Name = "Khoa học viễn tưởng",   Description = "Phim sci-fi, không gian, công nghệ" },
                new Genre { Name = "Tâm lý",               Description = "Phim tâm lý, kịch tính, trinh thám" },
                new Genre { Name = "Phiêu lưu",            Description = "Phim phiêu lưu, mạo hiểm" },
                new Genre { Name = "Tài liệu",             Description = "Phim tài liệu, lịch sử" }
            );
            await context.SaveChangesAsync();
        }

        // ── Seed Movies (15+ phim phong phú) ──────────────────
        if (!context.Movies.Any())
        {
            var genres = context.Genres.ToList();
            var actionGenre = genres.FirstOrDefault(g => g.Name == "Hành động");
            var comedyGenre = genres.FirstOrDefault(g => g.Name == "Hài hước");
            var horrorGenre = genres.FirstOrDefault(g => g.Name == "Kinh dị");
            var romanceGenre = genres.FirstOrDefault(g => g.Name == "Tình cảm");
            var animGenre   = genres.FirstOrDefault(g => g.Name == "Hoạt hình");
            var scifiGenre  = genres.FirstOrDefault(g => g.Name == "Khoa học viễn tưởng");
            var dramaGenre  = genres.FirstOrDefault(g => g.Name == "Tâm lý");
            var adventureGenre = genres.FirstOrDefault(g => g.Name == "Phiêu lưu");

            context.Movies.AddRange(
                // ── ĐANG CHIẾU (NowShowing) ──────────────────
                new Movie
                {
                    Title = "Avengers: Endgame",
                    PosterUrl = "/uploads/posters/poster_1.jpg",
                    Description = "Các siêu anh hùng Avengers tập hợp lần cuối cùng để chiến đấu với Thanos, cứu lấy vũ trụ khỏi sự hủy diệt trong trận chiến hoành tráng nhất lịch sử.",
                    Director = "Anthony & Joe Russo",
                    Actors = "Robert Downey Jr., Chris Evans, Scarlett Johansson, Chris Hemsworth",
                    Duration = 181,
                    ReleaseDate = DateTime.Today.AddDays(-30),
                    Status = MovieStatus.NowShowing,
                    Rating = 8.4m,
                    AgeRating = "C13",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=TcMBFSGVi1c",
                    GenreId = actionGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Spider-Man: Across the Spider-Verse",
                    PosterUrl = "/uploads/posters/poster_2.jpg",
                    Description = "Miles Morales trở lại trong cuộc phiêu lưu xuyên đa vũ trụ, gặp gỡ hàng loạt phiên bản Spider-Man và đối mặt với mối đe dọa lớn nhất từ trước tới nay.",
                    Director = "Joaquim Dos Santos, Kemp Powers",
                    Actors = "Shameik Moore, Hailee Steinfeld, Oscar Isaac",
                    Duration = 140,
                    ReleaseDate = DateTime.Today.AddDays(-14),
                    Status = MovieStatus.NowShowing,
                    Rating = 8.7m,
                    AgeRating = "P",
                    Language = "Lồng tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=cqGjhVJWtEg",
                    GenreId = animGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Oppenheimer",
                    PosterUrl = "/uploads/posters/poster_3.jpg",
                    Description = "Câu chuyện về J. Robert Oppenheimer - cha đẻ của bom nguyên tử, từ quá trình phát triển đến những hậu quả đạo đức mà ông phải đối mặt.",
                    Director = "Christopher Nolan",
                    Actors = "Cillian Murphy, Robert Downey Jr., Emily Blunt, Matt Damon",
                    Duration = 180,
                    ReleaseDate = DateTime.Today.AddDays(-21),
                    Status = MovieStatus.NowShowing,
                    Rating = 8.3m,
                    AgeRating = "C16",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=uYPbbksJxIg",
                    GenreId = dramaGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Doraemon: Nobita và Bản Giao Hưởng Địa Cầu",
                    PosterUrl = "/uploads/posters/poster_4.jpg",
                    Description = "Nobita và nhóm bạn tham gia vào cuộc phiêu lưu âm nhạc kỳ diệu, nơi tiếng nhạc có sức mạnh cứu lấy cả thế giới.",
                    Director = "Kazuaki Imai",
                    Actors = "Megumi Ōhara, Yumi Kakazu, Subaru Kimura",
                    Duration = 100,
                    ReleaseDate = DateTime.Today.AddDays(-10),
                    Status = MovieStatus.NowShowing,
                    Rating = 7.5m,
                    AgeRating = "P",
                    Language = "Lồng tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=u2CRGWRQR_Q",
                    GenreId = animGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Mai",
                    PosterUrl = "/uploads/posters/poster_5.jpg",
                    Description = "Câu chuyện tình cảm đầy cảm xúc về Mai - cô gái mạnh mẽ với quá khứ đau buồn và hành trình tìm lại hạnh phúc giữa Sài Gòn nhộn nhịp.",
                    Director = "Trấn Thành",
                    Actors = "Phương Anh Đào, Tuấn Trần, Trấn Thành, Hồng Đào",
                    Duration = 131,
                    ReleaseDate = DateTime.Today.AddDays(-20),
                    Status = MovieStatus.NowShowing,
                    Rating = 7.8m,
                    AgeRating = "C13",
                    Language = "Tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=2GXHMS2DBzE",
                    GenreId = romanceGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Quật Mộ Trùng Ma",
                    PosterUrl = "/uploads/posters/poster_6.jpg",
                    Description = "Một nhóm bạn trẻ vô tình đánh thức thế lực tà ác cổ xưa khi khám phá ngôi mộ bí ẩn giữa rừng sâu.",
                    Director = "Trần Hữu Tấn",
                    Actors = "Quốc Trường, Jun Vũ, Lê Giang",
                    Duration = 108,
                    ReleaseDate = DateTime.Today.AddDays(-7),
                    Status = MovieStatus.NowShowing,
                    Rating = 6.9m,
                    AgeRating = "C18",
                    Language = "Tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=8kHSHsXQPoE",
                    GenreId = horrorGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Fast & Furious X",
                    PosterUrl = "/uploads/posters/poster_7.jpg",
                    Description = "Dom Toretto cùng gia đình đối đầu với kẻ thù nguy hiểm nhất - Dante, kẻ khao khát trả thù và sẵn sàng phá hủy mọi thứ.",
                    Director = "Louis Leterrier",
                    Actors = "Vin Diesel, Jason Momoa, John Cena, Michelle Rodriguez",
                    Duration = 141,
                    ReleaseDate = DateTime.Today.AddDays(-25),
                    Status = MovieStatus.NowShowing,
                    Rating = 7.2m,
                    AgeRating = "C13",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=32RAq6JzY-w",
                    GenreId = actionGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Cô Dâu Hào Môn",
                    PosterUrl = "/uploads/posters/poster_8.jpg",
                    Description = "Câu chuyện hài hước về cô gái bình dân vô tình bước vào thế giới thượng lưu đầy phức tạp và rắc rối.",
                    Director = "Vũ Ngọc Đãng",
                    Actors = "Lan Ngọc, Kaity Nguyễn, Kiều Minh Tuấn",
                    Duration = 115,
                    ReleaseDate = DateTime.Today.AddDays(-5),
                    Status = MovieStatus.NowShowing,
                    Rating = 7.0m,
                    AgeRating = "C13",
                    Language = "Tiếng Việt",
                    GenreId = comedyGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Dune: Part Two",
                    PosterUrl = "/uploads/posters/poster_9.jpg",
                    Description = "Paul Atreides hợp lực với người Fremen để trả thù những kẻ đã phá hủy gia đình mình, đồng thời cố gắng ngăn chặn tương lai kinh hoàng mà chỉ mình anh nhìn thấy.",
                    Director = "Denis Villeneuve",
                    Actors = "Timothée Chalamet, Zendaya, Austin Butler, Florence Pugh",
                    Duration = 166,
                    ReleaseDate = DateTime.Today.AddDays(-18),
                    Status = MovieStatus.NowShowing,
                    Rating = 8.5m,
                    AgeRating = "C13",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=Way9Dexny3w",
                    GenreId = scifiGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Kung Fu Panda 4",
                    PosterUrl = "/uploads/posters/poster_10.jpg",
                    Description = "Po nhận nhiệm vụ mới: tìm kiếm và huấn luyện Dragon Warrior kế nhiệm trước khi đối đầu với phù thủy tắc kè hoa Chameleon.",
                    Director = "Mike Mitchell",
                    Actors = "Jack Black, Awkwafina, Viola Davis",
                    Duration = 94,
                    ReleaseDate = DateTime.Today.AddDays(-12),
                    Status = MovieStatus.NowShowing,
                    Rating = 7.1m,
                    AgeRating = "P",
                    Language = "Lồng tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=_inKs4eeHiI",
                    GenreId = animGenre?.GenreId,
                    IsActive = true
                },

                // ── SẮP CHIẾU (ComingSoon) ───────────────────
                new Movie
                {
                    Title = "Deadpool & Wolverine",
                    PosterUrl = "/uploads/posters/poster_11.jpg",
                    Description = "Deadpool kéo Wolverine ra khỏi cuộc sống ẩn dật để cùng nhau thực hiện nhiệm vụ chống lại kẻ thù chung đe dọa cả đa vũ trụ.",
                    Director = "Shawn Levy",
                    Actors = "Ryan Reynolds, Hugh Jackman",
                    Duration = 127,
                    ReleaseDate = DateTime.Today.AddDays(7),
                    Status = MovieStatus.ComingSoon,
                    AgeRating = "C18",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=73_1biulkYk",
                    GenreId = actionGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Inside Out 2",
                    PosterUrl = "/uploads/posters/poster_12.jpg",
                    Description = "Riley bước vào tuổi teen với những cảm xúc mới: Lo Lắng, Ghen Tị, Chán Nản và Xấu Hổ gia nhập đội cảm xúc, gây ra nhiều rắc rối bất ngờ.",
                    Director = "Kelsey Mann",
                    Actors = "Amy Poehler, Maya Hawke, Ayo Edebiri",
                    Duration = 96,
                    ReleaseDate = DateTime.Today.AddDays(14),
                    Status = MovieStatus.ComingSoon,
                    AgeRating = "P",
                    Language = "Lồng tiếng Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=LEjhY15eCx0",
                    GenreId = animGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Lật Mặt 7: Một Điều Ước",
                    PosterUrl = "/uploads/posters/poster_13.jpg",
                    Description = "Phần tiếp theo của series Lật Mặt với câu chuyện gia đình đầy xúc động và những bước ngoặt bất ngờ.",
                    Director = "Lý Hải",
                    Actors = "Trấn Thành, Ốc Thanh Vân, Quách Ngọc Tuyên",
                    Duration = 135,
                    ReleaseDate = DateTime.Today.AddDays(10),
                    Status = MovieStatus.ComingSoon,
                    AgeRating = "C13",
                    Language = "Tiếng Việt",
                    GenreId = dramaGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Godzilla x Kong: Đế Chế Mới",
                    PosterUrl = "/uploads/posters/poster_14.jpg",
                    Description = "Godzilla và Kong phải hợp tác chống lại mối đe dọa từ sâu trong lòng đất - nơi ẩn chứa bí mật về nguồn gốc của các Titan.",
                    Director = "Adam Wingard",
                    Actors = "Rebecca Hall, Brian Tyree Henry, Dan Stevens",
                    Duration = 115,
                    ReleaseDate = DateTime.Today.AddDays(3),
                    Status = MovieStatus.ComingSoon,
                    AgeRating = "C13",
                    Language = "Phụ đề Việt",
                    TrailerUrl = "https://www.youtube.com/watch?v=lV1OOlGwExM",
                    GenreId = actionGenre?.GenreId,
                    IsActive = true
                },
                new Movie
                {
                    Title = "Twisters",
                    PosterUrl = "/uploads/posters/poster_15.jpg",
                    Description = "Một chuyên gia khí tượng trở lại vùng đất lốc xoáy Oklahoma để thử nghiệm hệ thống cảnh báo sớm mới trong mùa bão kinh hoàng.",
                    Director = "Lee Isaac Chung",
                    Actors = "Daisy Edgar-Jones, Glen Powell",
                    Duration = 117,
                    ReleaseDate = DateTime.Today.AddDays(21),
                    Status = MovieStatus.ComingSoon,
                    AgeRating = "C13",
                    Language = "Phụ đề Việt",
                    GenreId = adventureGenre?.GenreId,
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // ── Ensure All Movies Have Valid Local Poster URLs ───────
        var allExistingMovies = await context.Movies.ToListAsync();
        bool anyMoviePosterFixed = false;
        foreach (var m in allExistingMovies)
        {
            if (string.IsNullOrWhiteSpace(m.PosterUrl) || !m.PosterUrl.StartsWith("/uploads/posters/"))
            {
                int pNum = m.MovieId;
                if (pNum < 1 || pNum > 15) pNum = ((m.MovieId - 1) % 15) + 1;
                m.PosterUrl = $"/uploads/posters/poster_{pNum}.jpg";
                anyMoviePosterFixed = true;
            }
        }
        foreach (var m in allExistingMovies)
        {
            bool isVn = m.Language != null && m.Language.Contains("Tiếng Việt", StringComparison.OrdinalIgnoreCase) && !m.Language.Contains("Phụ đề", StringComparison.OrdinalIgnoreCase) && !m.Language.Contains("Lồng tiếng", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(m.Country) || (m.Country == "Mỹ" && isVn))
            {
                m.Country = isVn ? "Việt Nam" : "Mỹ";
                anyMoviePosterFixed = true;
            }
            if (string.IsNullOrWhiteSpace(m.Format))
            {
                m.Format = m.Duration >= 140 ? "IMAX 2D" : "2D";
                anyMoviePosterFixed = true;
            }
            if (string.IsNullOrWhiteSpace(m.SubtitleType))
            {
                m.SubtitleType = isVn ? "Tiếng Việt gốc" : (m.Language ?? "Phụ đề Việt");
                anyMoviePosterFixed = true;
            }
        }
        if (anyMoviePosterFixed)
        {
            await context.SaveChangesAsync();
        }

        // ── 0. Seed Cinemas (Chuỗi cụm rạp thuộc hãng CineStar) ────
        if (!await context.Cinemas.AnyAsync())
        {
            context.Cinemas.AddRange(
                new Cinema
                {
                    Code = "CS-QT",
                    Name = "CineStar Quốc Thanh",
                    Address = "271 Nguyễn Trãi, Phường Nguyễn Cư Trinh, Quận 1, TP.HCM",
                    City = "TP. Hồ Chí Minh",
                    Hotline = "028 7300 8881",
                    Email = "quocthanh@cinestar.com.vn",
                    Description = "Cụm rạp trung tâm Quận 1 với hệ thống phòng chiếu chuẩn Dolby Surround 7.1 & IMAX Laser.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Cinema
                {
                    Code = "CS-HBT",
                    Name = "CineStar Hai Bà Trưng",
                    Address = "135 Hai Bà Trưng, Phường Bến Nghé, Quận 1, TP.HCM",
                    City = "TP. Hồ Chí Minh",
                    Hotline = "028 7300 8882",
                    Email = "haibatrung@cinestar.com.vn",
                    Description = "Tổ hợp rạp chiếu phim & giải trí hiện đại ngay trái tim Sài Gòn.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Cinema
                {
                    Code = "CS-BD",
                    Name = "CineStar Bình Dương (Sinh Viên)",
                    Address = "Nhà Văn Hóa Sinh Viên, ĐHQG TP.HCM, TP. Dĩ An, Bình Dương",
                    City = "Bình Dương",
                    Hotline = "028 7300 8883",
                    Email = "binhduong@cinestar.com.vn",
                    Description = "Cụm rạp hiện đại phục vụ khu đô thị Đại học Quốc gia TP.HCM.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }

        var allCinemas = await context.Cinemas.OrderBy(c => c.CinemaId).ToListAsync();
        var defaultCinemaId = allCinemas.FirstOrDefault()?.CinemaId;

        // ── 1. Seed SeatTypes (seat_types) ─────────────────────
        if (!context.SeatTypes.Any())
        {
            context.SeatTypes.AddRange(
                new SeatType
                {
                    Code = "STANDARD",
                    Name = "Ghế Thường",
                    Surcharge = 0m,
                    ColSpan = 1,
                    ColorCode = "#6c757d",
                    Description = "Ghế tiêu chuẩn êm ái, góc nhìn tốt.",
                    IsActive = true
                },
                new SeatType
                {
                    Code = "VIP",
                    Name = "Ghế VIP",
                    Surcharge = 20000m,
                    ColSpan = 1,
                    ColorCode = "#ffc107",
                    Description = "Ghế VIP khu vực trung tâm góc nhìn hoàn hảo, bọc da cao cấp.",
                    IsActive = true
                },
                new SeatType
                {
                    Code = "COUPLE",
                    Name = "Ghế Đôi",
                    Surcharge = 50000m,
                    ColSpan = 2,
                    ColorCode = "#e83e8c",
                    Description = "Ghế đôi Sweetbox riêng tư lãng mạn dành cho cặp đôi (chiếm 2 cột lưới).",
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        var seatTypeMap = await context.SeatTypes.ToDictionaryAsync(st => st.Code, st => st);
        var standardType = seatTypeMap["STANDARD"];
        var vipType = seatTypeMap["VIP"];
        var coupleType = seatTypeMap["COUPLE"];

        // ── 2. Seed Screening Rooms & Seats (Có Hành lang & Ghế đôi) ──
        if (!context.ScreeningRooms.Any())
        {
            var rooms = new List<ScreeningRoom>
            {
                new ScreeningRoom
                {
                    RoomName = "Phòng 1 - Standard",
                    RoomType = RoomType.Standard,
                    TotalRows = 10,
                    TotalCols = 16,
                    Description = "Phòng chiếu tiêu chuẩn kỹ thuật số 2D âm thanh Dolby Surround 7.1",
                    IsActive = true
                },
                new ScreeningRoom
                {
                    RoomName = "Phòng 2 - VIP Gold",
                    RoomType = RoomType.VIP,
                    TotalRows = 10,
                    TotalCols = 16,
                    Description = "Phòng chiếu VIP cao cấp ghế ngả điện bọc da nhập khẩu",
                    IsActive = true
                },
                new ScreeningRoom
                {
                    RoomName = "Phòng 3 - IMAX Laser",
                    RoomType = RoomType.IMAX,
                    TotalRows = 10,
                    TotalCols = 16,
                    Description = "Màn chiếu IMAX Laser khổng lồ âm thanh Dolby Atmos đa tầng",
                    IsActive = true
                }
            };
            context.ScreeningRooms.AddRange(rooms);
            await context.SaveChangesAsync();

            // Sinh ma trận ghế thực tế có hành lang:
            // Ma trận 10 hàng x 16 cột
            // - Hàng ngang GridRow = 4 là hành lang ngăn cách Standard (hàng 1..3) và VIP (hàng 5..9)
            // - Cột dọc GridCol = 4 và GridCol = 13 là 2 lối đi bộ dọc phòng
            // - Hàng cuối GridRow = 10 là ghế Couple (ColSpan = 2), đặt tại các GridCol = 1, 5, 7, 9, 11, 14
            var allNewSeats = new List<Seat>();
            var rowLetters = new Dictionary<int, string>
            {
                { 1, "A" }, { 2, "B" }, { 3, "C" },
                // 4 là hành lang
                { 5, "D" }, { 6, "E" }, { 7, "F" }, { 8, "G" }, { 9, "H" },
                { 10, "J" }
            };

            foreach (var room in rooms)
            {
                for (int r = 1; r <= room.TotalRows; r++)
                {
                    if (r == 4) continue; // Hành lang ngang, không tạo ghế

                    var rowLetter = rowLetters[r];

                    if (r == 10)
                    {
                        // Hàng ghế đôi: ColSpan = 2, cách nhau 2 đơn vị GridCol
                        var coupleCols = new[] { 1, 5, 7, 9, 11, 14 };
                        int coupleSeatNum = 1;

                        foreach (var col in coupleCols)
                        {
                            allNewSeats.Add(new Seat
                            {
                                RoomId = room.RoomId,
                                RowLabel = rowLetter,
                                SeatNumber = coupleSeatNum++,
                                GridRow = r,
                                GridCol = col,
                                SeatTypeId = coupleType.SeatTypeId,
                                SeatTypeLegacy = SeatTypeEnum.Couple,
                                IsActive = true
                            });
                        }
                    }
                    else
                    {
                        // Hàng ghế đơn (Standard hoặc VIP)
                        var currentType = r <= 3 ? standardType : vipType;
                        var currentTypeEnum = r <= 3 ? SeatTypeEnum.Standard : SeatTypeEnum.VIP;
                        int seatNum = 1;

                        for (int c = 1; c <= room.TotalCols; c++)
                        {
                            // 2 hành lang dọc tại cột 4 và cột 13
                            if (c == 4 || c == 13) continue;

                            allNewSeats.Add(new Seat
                            {
                                RoomId = room.RoomId,
                                RowLabel = rowLetter,
                                SeatNumber = seatNum++,
                                GridRow = r,
                                GridCol = c,
                                SeatTypeId = currentType.SeatTypeId,
                                SeatTypeLegacy = currentTypeEnum,
                                IsActive = true
                            });
                        }
                    }
                }
            }

            context.Seats.AddRange(allNewSeats);
            await context.SaveChangesAsync();
        }

        // Phân bổ các phòng chiếu vào các cụm rạp thuộc hãng nếu chưa gắn CinemaId
        var existingRooms = await context.ScreeningRooms.OrderBy(r => r.RoomId).ToListAsync();
        if (allCinemas.Count > 0 && existingRooms.Any(r => r.CinemaId == null))
        {
            for (int i = 0; i < existingRooms.Count; i++)
            {
                if (existingRooms[i].CinemaId == null)
                {
                    existingRooms[i].CinemaId = allCinemas[i % allCinemas.Count].CinemaId;
                }
            }
            await context.SaveChangesAsync();
        }

        // ── 3. Seed Showtimes (7 ngày, nhiều suất chiếu) ───────
        if (!context.Showtimes.Any())
        {
            var movies = context.Movies.Where(m => m.Status == MovieStatus.NowShowing && m.IsActive).ToList();
            var rooms = context.ScreeningRooms.ToList();

            if (rooms.Any() && movies.Any())
            {
                var showtimes = new List<Showtime>();
                var prices = new Dictionary<int, decimal>();
                foreach (var rm in rooms)
                {
                    if (rm.RoomName.Contains("Standard")) prices[rm.RoomId] = 90000m;
                    else if (rm.RoomName.Contains("VIP")) prices[rm.RoomId] = 130000m;
                    else prices[rm.RoomId] = 180000m;
                }

                // Generate showtimes for 7 days (yesterday through +5 days)
                for (int dayOffset = -1; dayOffset <= 5; dayOffset++)
                {
                    var date = DateTime.Today.AddDays(dayOffset);
                    int movieIndex = 0;

                    foreach (var room in rooms)
                    {
                        // 5 suất mỗi phòng mỗi ngày: 9h, 12h, 15h, 18h30, 21h
                        var timeSlots = new[] { 9.0, 12.0, 15.0, 18.5, 21.0 };
                        foreach (var hour in timeSlots)
                        {
                            var movie = movies[movieIndex % movies.Count];
                            movieIndex++;

                            showtimes.Add(new Showtime
                            {
                                MovieId = movie.MovieId,
                                RoomId = room.RoomId,
                                StartTime = date.AddHours(hour),
                                EndTime = date.AddHours(hour).AddMinutes(movie.Duration + 15),
                                TicketPrice = prices.GetValueOrDefault(room.RoomId, 90000m),
                                Format = room.RoomName.Contains("IMAX") ? "IMAX" : "2D",
                                SubtitleType = movie.Language ?? "Phụ đề",
                                Status = ShowtimeStatus.Available
                            });
                        }
                    }
                }

                context.Showtimes.AddRange(showtimes);
                await context.SaveChangesAsync();
            }
        }
        else
        {
            // Auto-reschedule: if ALL Available showtimes are in the past, shift them to start from today.
            var availableShowtimes = context.Showtimes
                .Where(s => s.Status == ShowtimeStatus.Available)
                .ToList();

            bool allPast = availableShowtimes.Any() && availableShowtimes.All(s => s.StartTime < DateTime.Now);
            if (allPast)
            {
                var oldest = availableShowtimes.Min(s => s.StartTime.Date);
                int daysDiff = (DateTime.Today - oldest).Days;
                foreach (var st in availableShowtimes)
                {
                    st.StartTime = st.StartTime.AddDays(daysDiff);
                    if (st.EndTime.HasValue)
                        st.EndTime = st.EndTime.Value.AddDays(daysDiff);
                }
                await context.SaveChangesAsync();
            }
        }

        // ── 4. Tự động sinh ShowtimeSeats cho các suất chiếu ──
        var allShowtimesList = await context.Showtimes.ToListAsync();
        var showtimesWithoutSeats = allShowtimesList
            .Where(st => !context.ShowtimeSeats.Any(ss => ss.ShowtimeId == st.ShowtimeId))
            .ToList();

        if (showtimesWithoutSeats.Any())
        {
            var seatsWithTypes = await context.Seats
                .Where(s => s.IsActive)
                .Include(s => s.SeatType)
                .ToListAsync();

            var seatsByRoom = seatsWithTypes.GroupBy(s => s.RoomId).ToDictionary(g => g.Key, g => g.ToList());
            var newShowtimeSeats = new List<ShowtimeSeat>();

            foreach (var st in showtimesWithoutSeats)
            {
                if (seatsByRoom.TryGetValue(st.RoomId, out var roomSeats))
                {
                    foreach (var seat in roomSeats)
                    {
                        decimal surcharge = seat.SeatType?.Surcharge ?? 
                            (seat.SeatTypeLegacy == SeatTypeEnum.VIP ? 20000m : seat.SeatTypeLegacy == SeatTypeEnum.Couple ? 50000m : 0m);

                        newShowtimeSeats.Add(new ShowtimeSeat
                        {
                            ShowtimeId = st.ShowtimeId,
                            SeatId = seat.SeatId,
                            SeatTypeId = seat.SeatTypeId,
                            Price = st.TicketPrice + surcharge,
                            Status = ShowtimeSeatStatus.Available,
                            Version = 1,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            if (newShowtimeSeats.Any())
            {
                context.ShowtimeSeats.AddRange(newShowtimeSeats);
                await context.SaveChangesAsync();
            }
        }

        // ── Seed F&B Items ─────────────────────────────────────
        if (!context.FoodItems.Any())
        {
            context.FoodItems.AddRange(
                new FoodItem { Name = "Bắp rang bơ nhỏ",           Category = FoodCategory.Popcorn, Price = 49000,  Description = "Bắp rang bơ size nhỏ thơm ngon", ImageUrl = "/uploads/fnb/fnb_1.jpg", IsAvailable = true },
                new FoodItem { Name = "Bắp rang bơ vừa",           Category = FoodCategory.Popcorn, Price = 65000,  Description = "Bắp rang bơ size vừa", ImageUrl = "/uploads/fnb/fnb_2.jpg", IsAvailable = true },
                new FoodItem { Name = "Bắp rang bơ lớn",           Category = FoodCategory.Popcorn, Price = 79000,  Description = "Bắp rang bơ size lớn", ImageUrl = "/uploads/fnb/fnb_3.jpg", IsAvailable = true },
                new FoodItem { Name = "Bắp rang caramel",          Category = FoodCategory.Popcorn, Price = 69000,  Description = "Bắp rang vị caramel ngọt ngào", ImageUrl = "/uploads/fnb/fnb_4.jpg", IsAvailable = true },
                new FoodItem { Name = "Coca-Cola 22oz",            Category = FoodCategory.Drink,   Price = 39000,  Description = "Nước ngọt Coca-Cola size 22oz", ImageUrl = "/uploads/fnb/fnb_5.jpg", IsAvailable = true },
                new FoodItem { Name = "Pepsi 22oz",                Category = FoodCategory.Drink,   Price = 39000,  Description = "Nước ngọt Pepsi size 22oz", ImageUrl = "/uploads/fnb/fnb_6.jpg", IsAvailable = true },
                new FoodItem { Name = "Nước lọc",                  Category = FoodCategory.Drink,   Price = 20000,  Description = "Nước lọc đóng chai 500ml", ImageUrl = "/uploads/fnb/fnb_7.jpg", IsAvailable = true },
                new FoodItem { Name = "Trà đào",                   Category = FoodCategory.Drink,   Price = 45000,  Description = "Trà đào cam sả mát lạnh", ImageUrl = "/uploads/fnb/fnb_8.jpg", IsAvailable = true },
                new FoodItem { Name = "Combo 1: Bắp nhỏ + Coca",  Category = FoodCategory.Combo,   Price = 79000,  Description = "1 bắp rang nhỏ + 1 Coca-Cola 22oz", ImageUrl = "/uploads/fnb/fnb_9.jpg", IsAvailable = true },
                new FoodItem { Name = "Combo 2: Bắp lớn + 2 Coca",Category = FoodCategory.Combo,   Price = 149000, Description = "1 bắp rang lớn + 2 Coca-Cola 22oz", ImageUrl = "/uploads/fnb/fnb_10.jpg", IsAvailable = true },
                new FoodItem { Name = "Combo Couple: 2 Bắp vừa + 2 nước", Category = FoodCategory.Combo, Price = 189000, Description = "2 bắp rang vừa + 2 nước tùy chọn", ImageUrl = "/uploads/fnb/fnb_11.jpg", IsAvailable = true },
                new FoodItem { Name = "Nachos phô mai",            Category = FoodCategory.Snack,   Price = 55000,  Description = "Nachos giòn với sốt phô mai béo ngậy", ImageUrl = "/uploads/fnb/fnb_12.jpg", IsAvailable = true },
                new FoodItem { Name = "Hotdog",                    Category = FoodCategory.Snack,   Price = 45000,  Description = "Xúc xích nướng kẹp bánh mì", ImageUrl = "/uploads/fnb/fnb_13.jpg", IsAvailable = true },
                new FoodItem { Name = "Gà rán (3 miếng)",          Category = FoodCategory.Snack,   Price = 75000,  Description = "Gà rán giòn kiểu Hàn Quốc", ImageUrl = "/uploads/fnb/fnb_14.jpg", IsAvailable = true }
            );
            await context.SaveChangesAsync();
        }

        // ── Ensure All FoodItems Have Valid Local Image URLs ──────
        var allExistingFoods = await context.FoodItems.ToListAsync();
        bool anyFoodImageFixed = false;
        foreach (var f in allExistingFoods)
        {
            if (string.IsNullOrWhiteSpace(f.ImageUrl) || !f.ImageUrl.StartsWith("/uploads/fnb/"))
            {
                int fNum = f.FoodItemId;
                if (fNum < 1 || fNum > 14) fNum = ((f.FoodItemId - 1) % 14) + 1;
                f.ImageUrl = $"/uploads/fnb/fnb_{fNum}.jpg";
                anyFoodImageFixed = true;
            }
        }
        if (anyFoodImageFixed)
        {
            await context.SaveChangesAsync();
        }

        // ── Seed Vouchers (Sprint 3) ────────────────────────────
        if (!context.Vouchers.Any())
        {
            context.Vouchers.AddRange(
                new Voucher
                {
                    Code = "CINEMA10",
                    Description = "Giảm 10% hóa đơn đặt vé",
                    DiscountPercent = 10,
                    MaxDiscountAmount = 50000,
                    ValidFrom = DateTime.Today.AddDays(-30),
                    ValidTo = DateTime.Today.AddDays(90),
                    IsActive = true
                },
                new Voucher
                {
                    Code = "NEWUSER20",
                    Description = "Ưu đãi khách hàng mới – Giảm 20%",
                    DiscountPercent = 20,
                    MaxDiscountAmount = 100000,
                    ValidFrom = DateTime.Today.AddDays(-30),
                    ValidTo = DateTime.Today.AddDays(60),
                    UsageLimit = 100,
                    IsActive = true
                },
                new Voucher
                {
                    Code = "VIP30",
                    Description = "Ưu đãi VIP – Giảm 30% (tối đa 150k)",
                    DiscountPercent = 30,
                    MaxDiscountAmount = 150000,
                    ValidFrom = DateTime.Today.AddDays(-15),
                    ValidTo = DateTime.Today.AddDays(60),
                    IsActive = true
                },
                new Voucher
                {
                    Code = "WEEKEND15",
                    Description = "Cuối tuần vui vẻ – Giảm 15%",
                    DiscountPercent = 15,
                    MaxDiscountAmount = 80000,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddDays(90),
                    IsActive = true
                },
                new Voucher
                {
                    Code = "SUMMER50K",
                    Description = "Mùa hè sôi động – Giảm 50.000đ",
                    DiscountType = DiscountType.FixedAmount,
                    DiscountAmount = 50000,
                    MinOrderAmount = 200000,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddDays(45),
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // ── Seed Demo Booking History ───────────────────────────
        // Only seed if no bookings exist yet
        if (!context.Bookings.Any() && customerUsers.Count >= 3)
        {
            var staffUser = await userManager.FindByEmailAsync("staff@cinema.com");
            var pastShowtimes = context.Showtimes
                .Where(s => s.StartTime < DateTime.Now)
                .Include(s => s.Room)
                .OrderBy(s => s.StartTime)
                .Take(12)
                .ToList();

            if (!pastShowtimes.Any())
            {
                // Create a few past showtimes for demo
                var movie1 = context.Movies.FirstOrDefault(m => m.Status == MovieStatus.NowShowing);
                var roomStd = context.ScreeningRooms.FirstOrDefault(r => r.RoomName.Contains("Standard"));
                if (movie1 != null && roomStd != null)
                {
                    for (int d = 5; d >= 1; d--)
                    {
                        pastShowtimes.Add(new Showtime
                        {
                            MovieId = movie1.MovieId,
                            RoomId = roomStd.RoomId,
                            StartTime = DateTime.Today.AddDays(-d).AddHours(14),
                            TicketPrice = 90000,
                            Status = ShowtimeStatus.Available
                        });
                    }
                    context.Showtimes.AddRange(pastShowtimes.Where(s => s.ShowtimeId == 0));
                    await context.SaveChangesAsync();
                    // Reload
                    pastShowtimes = context.Showtimes
                        .Where(s => s.StartTime < DateTime.Now)
                        .OrderBy(s => s.StartTime)
                        .Take(12)
                        .ToList();
                }
            }

            var allSeats = context.Seats.ToList();
            var vouchers = context.Vouchers.ToList();
            var foodItems = context.FoodItems.ToList();
            int bookingCount = 0;

            foreach (var showtime in pastShowtimes.Take(8))
            {
                // Pick a customer
                var custUser = customerUsers[bookingCount % customerUsers.Count];
                if (custUser == null) continue;

                var roomSeats = allSeats.Where(s => s.RoomId == showtime.RoomId && s.IsActive).ToList();
                if (!roomSeats.Any()) continue;

                // Pick 2-4 random seats
                var rng = new Random(showtime.ShowtimeId + bookingCount);
                int seatCount = rng.Next(2, 5);
                var selectedSeats = roomSeats
                    .OrderBy(_ => rng.Next())
                    .Take(seatCount)
                    .ToList();

                decimal ticketAmount = selectedSeats.Count * showtime.TicketPrice;

                // Maybe add F&B
                decimal fnbAmount = 0;
                var selectedFoods = new List<(FoodItem item, int qty)>();
                if (rng.Next(100) < 70) // 70% chance
                {
                    var food = foodItems[rng.Next(foodItems.Count)];
                    int qty = rng.Next(1, 3);
                    fnbAmount = food.Price * qty;
                    selectedFoods.Add((food, qty));
                }

                decimal totalAmount = ticketAmount + fnbAmount;

                // Maybe apply voucher
                decimal discountAmount = 0;
                Voucher? usedVoucher = null;
                if (rng.Next(100) < 40 && vouchers.Any()) // 40% chance
                {
                    usedVoucher = vouchers[rng.Next(vouchers.Count)];
                    if (usedVoucher.DiscountType == DiscountType.Percent)
                    {
                        discountAmount = totalAmount * usedVoucher.DiscountPercent / 100m;
                        if (usedVoucher.MaxDiscountAmount.HasValue)
                            discountAmount = Math.Min(discountAmount, usedVoucher.MaxDiscountAmount.Value);
                    }
                    else
                    {
                        discountAmount = usedVoucher.DiscountAmount;
                    }
                }

                decimal finalAmount = Math.Max(0, totalAmount - discountAmount);

                var bookingCode = $"BK-{DateTime.Today:yyyyMMdd}-{bookingCount + 1:D4}";
                var bookingTime = showtime.StartTime.AddHours(-rng.Next(1, 48));

                var booking = new Booking
                {
                    UserId = custUser.Id,
                    ShowtimeId = showtime.ShowtimeId,
                    VoucherId = usedVoucher?.VoucherId,
                    BookingCode = bookingCode,
                    BookingTime = bookingTime,
                    TicketAmount = ticketAmount,
                    FnBAmount = fnbAmount,
                    TotalAmount = totalAmount,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = (PaymentMethod)(rng.Next(3)),
                    PaymentReference = $"PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    PaidAt = bookingTime.AddMinutes(rng.Next(1, 10)),
                    ContactEmail = custUser.Email,
                    ContactPhone = custUser.PhoneNumber,
                    TicketEmailSent = true,
                    TicketEmailSentAt = bookingTime.AddMinutes(rng.Next(10, 30)),
                    CreatedAt = bookingTime
                };

                context.Bookings.Add(booking);
                await context.SaveChangesAsync();

                // Cập nhật trạng thái ShowtimeSeats sang Reserved
                var bookedSeatIds = selectedSeats.Select(s => s.SeatId).ToHashSet();
                var stSeatsToReserve = await context.ShowtimeSeats
                    .Where(ss => ss.ShowtimeId == showtime.ShowtimeId && bookedSeatIds.Contains(ss.SeatId))
                    .ToListAsync();
                foreach (var ss in stSeatsToReserve)
                {
                    ss.Status = ShowtimeSeatStatus.Reserved;
                    ss.Version++;
                }

                // Create tickets for each seat (liên kết khóa ngoại ShowtimeSeatId)
                int ticketIdx = 0;
                foreach (var seat in selectedSeats)
                {
                    var ticketCode = $"TCK-{bookingCode[3..]}-{ticketIdx + 1:D2}";
                    var qrPayload = $"cinema-ticket:{booking.BookingId}:{ticketCode}";
                    var matchedStSeat = stSeatsToReserve.FirstOrDefault(ss => ss.SeatId == seat.SeatId);

                    // Some tickets have been checked in (past showtimes)
                    bool isCheckedIn = showtime.StartTime < DateTime.Now.AddHours(-2) && rng.Next(100) < 80;

                    context.Tickets.Add(new Ticket
                    {
                        BookingId = booking.BookingId,
                        ShowtimeId = showtime.ShowtimeId,
                        SeatId = seat.SeatId,
                        ShowtimeSeatId = matchedStSeat?.ShowtimeSeatId,
                        TicketCode = ticketCode,
                        SeatCode = $"{seat.RowLabel}{seat.SeatNumber}",
                        UnitPrice = showtime.TicketPrice,
                        Status = isCheckedIn ? TicketStatus.CheckedIn : TicketStatus.Issued,
                        QrPayload = qrPayload,
                        CheckedInAt = isCheckedIn ? showtime.StartTime.AddMinutes(rng.Next(-15, 5)) : null,
                        CheckedInByUserId = isCheckedIn ? staffUser?.Id : null,
                        CreatedAt = bookingTime
                    });
                    ticketIdx++;
                }

                // 10 Bảng chuẩn: Thêm Payment
                context.Payments.Add(new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.FinalAmount,
                    Method = booking.PaymentMethod ?? PaymentMethod.Cash,
                    Provider = PaymentProvider.VietQR,
                    TransactionCode = booking.PaymentReference,
                    Status = PaymentStatus.Paid,
                    PaidAt = booking.PaidAt,
                    GatewayResponse = "{\"code\":\"00\",\"desc\":\"Thanh toan VietQR Napas 247 thanh cong\"}",
                    CreatedAt = booking.BookingTime
                });

                // 10 Bảng chuẩn: Thêm BookingConcessions
                foreach (var (food, qty) in selectedFoods)
                {
                    context.BookingConcessions.Add(new BookingConcession
                    {
                        BookingId = booking.BookingId,
                        FoodItemId = food.FoodItemId,
                        Quantity = qty,
                        UnitPrice = food.Price,
                        ItemName = food.Name,
                        CreatedAt = bookingTime
                    });
                }
                await context.SaveChangesAsync();
                bookingCount++;
            }
        }

        // ── Seed Rich Demo Reviews for All Movies (SCRUM-28) ────
        if (context.Reviews.Count() < 25 && customerUsers.Any())
        {
            var userDict = customerUsers.Where(u => !string.IsNullOrEmpty(u.Email))
                                        .ToDictionary(u => u.Email!.ToLowerInvariant(), u => u);

            var allMoviesForReviews = await context.Movies.ToListAsync();

            var richReviewSeeds = new (string MovieTitle, string UserEmail, int Rating, string Comment, ReviewStatus Status, int DaysAgo)[]
            {
                // 1. Avengers: Endgame
                ("Avengers: Endgame", "nguyenvana@gmail.com", 5, "Trận đại chiến cuối cùng quá mãn nhãn và xúc động! Khoảnh khắc Avengers Assemble và sự hy sinh của Iron Man khiến cả rạp vỡ òa nước mắt. Phim hay nhất của vũ trụ Marvel!", ReviewStatus.Published, 14),
                ("Avengers: Endgame", "tranthib@gmail.com", 5, "Đỉnh cao điện ảnh siêu anh hùng! Kỹ xảo CGI 10/10, âm thanh Dolby Atmos tại rạp nghe rung chuyển ghế. Rất xứng đáng với 3 tiếng ngồi rạp!", ReviewStatus.Published, 10),
                ("Avengers: Endgame", "levanc@gmail.com", 4, "Phim rất xúc động, mạch phim phần đầu hơi chậm nhưng nửa sau bùng nổ nghẹt thở. Cảm ơn Marvel vì một tuổi thơ tuyệt vời.", ReviewStatus.Published, 5),
                ("Avengers: Endgame", "phamthid@gmail.com", 5, "Xem lại lần thứ 2 tại rạp vẫn nổi da gà. Diễn xuất của Robert Downey Jr. và Chris Evans quá xuất sắc.", ReviewStatus.Published, 2),

                // 2. Spider-Man: Across the Spider-Verse
                ("Spider-Man: Across the Spider-Verse", "levanc@gmail.com", 5, "Đồ họa và phong cách hoạt hình vượt trên mọi chuẩn mực! Mỗi vũ trụ một phong cách vẽ riêng biệt cực kỳ sáng tạo. Nhạc phim và cốt truyện cuốn hút từ đầu đến cuối.", ReviewStatus.Published, 18),
                ("Spider-Man: Across the Spider-Verse", "tranthib@gmail.com", 5, "Một kiệt tác nghệ thuật thị giác! Diễn biến tâm lý của Miles Morales và Gwen Stacy chạm tới cảm xúc. Xem trên màn hình lớn thực sự choáng ngợp!", ReviewStatus.Published, 12),
                ("Spider-Man: Across the Spider-Verse", "vuthimai@gmail.com", 4, "Phim siêu cuốn, màu sắc sống động bùng nổ thị giác. Điểm trừ duy nhất là kết thúc mở để chờ phần tiếp theo làm háo hức quá.", ReviewStatus.Published, 6),

                // 3. Oppenheimer
                ("Oppenheimer", "hoange@gmail.com", 5, "Đúng chất bậc thầy Christopher Nolan! Âm thanh vụ nổ Trinity khiến cả rạp nín thở thót tim. Cillian Murphy diễn xuất bằng ánh mắt quá đỉnh cao, xứng đáng tượng vàng Oscar.", ReviewStatus.Published, 20),
                ("Oppenheimer", "doquanghuy@gmail.com", 5, "Phim lịch sử khoa học kịch tính như phim hành động. Âm nhạc của Ludwig Göransson dồn dập tạo cảm giác căng thẳng tột độ.", ReviewStatus.Published, 15),
                ("Oppenheimer", "nguyenvana@gmail.com", 4, "Hội thoại dày đặc đòi hỏi tập trung cao độ, nhưng từng chi tiết đều đắt giá. Một tác phẩm điện ảnh kinh điển thực thụ.", ReviewStatus.Published, 8),
                ("Oppenheimer", "buithutrang@gmail.com", 1, "Phim nhiều lời thoại và thuật ngữ khoa học quá, hơi khó theo dõi với người xem phổ thông...", ReviewStatus.Hidden, 7),

                // 4. Doraemon: Nobita và Bản Giao Hưởng Địa Cầu
                ("Doraemon: Nobita và Bản Giao Hưởng Địa Cầu", "tranthib@gmail.com", 5, "Phim hoạt hình dễ thương, âm nhạc giao hưởng du dương tuyệt vời! Cả người lớn lẫn trẻ em đều thích mê. Rất thích hợp đi xem cùng gia đình cuối tuần.", ReviewStatus.Published, 16),
                ("Doraemon: Nobita và Bản Giao Hưởng Địa Cầu", "buithutrang@gmail.com", 4, "Đưa bé nhà mình đi xem mà mẹ cũng khóc theo. Tình bạn của nhóm bạn Nobita và thông điệp âm nhạc kết nối trái đất rất ý nghĩa.", ReviewStatus.Published, 9),
                ("Doraemon: Nobita và Bản Giao Hưởng Địa Cầu", "levanc@gmail.com", 5, "Hình ảnh vẽ cực đẹp, âm thanh rạp chiếu sống động. Mỗi mùa hè được xem một phần Doraemon là niềm vui trọn vẹn của tuổi thơ.", ReviewStatus.Published, 3),

                // 5. Mai
                ("Mai", "phamthid@gmail.com", 5, "Phim xúc động sâu sắc, phản ánh chân thực số phận người phụ nữ và áp lực gia đình. Phương Anh Đào và Tuấn Trần diễn xuất quá ăn ý, khóc ướt cả khăn giấy.", ReviewStatus.Published, 22),
                ("Mai", "nguyenphuctien@gmail.com", 4, "Góc quay và màu phim rất đẹp, chất lượng âm thanh tốt. Kịch bản lắng đọng, câu thoại thấm thía để lại nhiều suy ngẫm sau khi bước ra khỏi rạp.", ReviewStatus.Published, 13),
                ("Mai", "tranthib@gmail.com", 4, "Trấn Thành đã làm một bộ phim rất chỉn chu và cảm xúc. Rạp CineStar phục vụ bắp nước ngon, ghế ngồi êm ái.", ReviewStatus.Published, 4),

                // 6. Quật Mộ Trùng Ma
                ("Quật Mộ Trùng Ma", "doquanghuy@gmail.com", 5, "Phim kinh dị tâm linh Hàn Quốc quá xuất sắc! Yếu tố phong thủy, trừ tà và lịch sử đan xen nghẹt thở. Kim Go Eun và Lee Do Hyun diễn đỉnh chóp!", ReviewStatus.Published, 25),
                ("Quật Mộ Trùng Ma", "vuthimai@gmail.com", 5, "Không khí u ám, rùng rợn bao trùm từ đầu đến cuối. Âm thanh rạp hiệu ứng vòm chân thực làm giật thót tim mấy phen. Đáng tiền vé!", ReviewStatus.Published, 17),
                ("Quật Mộ Trùng Ma", "nguyenvana@gmail.com", 4, "Nửa đầu phim không khí cực kỳ cuốn hút và ma mị, nửa sau hơi hướng hành động nhưng vẫn rất hồi hộp và thỏa mãn.", ReviewStatus.Published, 11),

                // 7. Fast & Furious X
                ("Fast & Furious X", "nguyenphuctien@gmail.com", 4, "Phim hành động thuần giải trí đỉnh cao! Những pha đua xe cháy nổ bất chấp vật lý xem cực kỳ đã mắt. Jason Momoa đóng phản diện quái chiêu rất cuốn hút.", ReviewStatus.Published, 19),
                ("Fast & Furious X", "levanc@gmail.com", 4, "Xem rạp âm thanh gầm rú của động cơ xe phê chữ ê kéo dài. Đi cùng hội bạn xem giải trí cuối tuần xả stress cực chuẩn.", ReviewStatus.Published, 14),
                ("Fast & Furious X", "hoange@gmail.com", 3, "Kịch bản hơi phi lý nhưng xem kỹ xảo hành động thì vẫn rất sướng và mãn nhãn.", ReviewStatus.Published, 6),

                // 8. Cô Dâu Hào Môn
                ("Cô Dâu Hào Môn", "buithutrang@gmail.com", 4, "Kịch bản trào phúng thú vị về gia đình giả giàu để đổi đời. Diễn xuất của Uyển Ân và Thu Trang rất duyên dáng, mang lại nhiều tiếng cười sảng khoái.", ReviewStatus.Published, 12),
                ("Cô Dâu Hào Môn", "phamthid@gmail.com", 4, "Phục trang và bối cảnh xa hoa lộng lẫy. Phim nhẹ nhàng, hài hước, xem vừa giải trí vừa thấy được giá trị của tình cảm gia đình.", ReviewStatus.Published, 5),

                // 9. Dune: Part Two
                ("Dune: Part Two", "hoange@gmail.com", 5, "Tuyệt phẩm khoa học viễn tưởng vĩ đại của thập kỷ! Sa mạc Arrakis hiện lên kỳ vĩ, đàn sâu cát khổng lồ choáng ngợp. Bắt buộc phải xem trên màn hình lớn!", ReviewStatus.Published, 24),
                ("Dune: Part Two", "nguyenvana@gmail.com", 5, "Âm thanh của Hans Zimmer gầm vang rung chuyển cả lồng ngực. Diễn xuất của Timothée Chalamet và Zendaya thăng hoa tột bậc.", ReviewStatus.Published, 16),
                ("Dune: Part Two", "doquanghuy@gmail.com", 5, "Xứng đáng từng xu tiền vé! Mãn nhãn về cả phần nhìn lẫn phần nghe, nhịp phim kịch tính không rời mắt được một giây.", ReviewStatus.Published, 8),

                // 10. Kung Fu Panda 4
                ("Kung Fu Panda 4", "tranthib@gmail.com", 5, "Gấu Po trở lại vẫn đáng yêu và tấu hài như ngày nào! Các màn võ thuật kết hợp hoạt hình mượt mà, rạp chiếu đông vui cười rôm rả.", ReviewStatus.Published, 15),
                ("Kung Fu Panda 4", "buithutrang@gmail.com", 4, "Phim ngắn gọn, vui nhộn, hình ảnh rực rỡ sắc màu. Các bạn nhỏ đi xem thích mê, combo bắp nước ngọt ngào rất ngon.", ReviewStatus.Published, 7),

                // 11. Deadpool & Wolverine
                ("Deadpool & Wolverine", "nguyenvana@gmail.com", 5, "Cặp đôi hoàn hảo Ryan Reynolds và Hugh Jackman! Hài bựa cực đỉnh, các màn cameo bất ngờ khiến cả rạp hò reo phấn khích. Phim Marvel đỉnh nhất năm!", ReviewStatus.Published, 18),
                ("Deadpool & Wolverine", "levanc@gmail.com", 5, "Đánh đấm bạo lực máu lửa đúng chuẩn 18+, nhạc phim thập niên 90-2000 chọn quá chất. Cười nghiêng ngả từ phút đầu đến tận cảnh after-credit.", ReviewStatus.Published, 10),
                ("Deadpool & Wolverine", "nguyenphuctien@gmail.com", 5, "Xem giải trí đã đời luôn! Fan cứng Marvel không thể bỏ qua bộ phim này.", ReviewStatus.Published, 4),
                ("Deadpool & Wolverine", "hoange@gmail.com", 5, "[SPOILER NỘI DUNG] Xuất hiện cả Blade và Gambit chiến đấu cùng nhau cực kỳ mãn nhãn!", ReviewStatus.Hidden, 3),

                // 12. Inside Out 2
                ("Inside Out 2", "phamthid@gmail.com", 5, "Pixar không bao giờ làm khán giả thất vọng! Nhân vật Lo Âu (Anxiety) được xây dựng quá chân thực, xem mà thấy đồng cảm sâu sắc. Rơi nước mắt vì thấy lại chính mình.", ReviewStatus.Published, 21),
                ("Inside Out 2", "vuthimai@gmail.com", 5, "Một bài học tâm lý trưởng thành vô cùng nhân văn cho cả người lớn. Phim sâu sắc, ấm áp và đong đầy tình yêu thương.", ReviewStatus.Published, 14),
                ("Inside Out 2", "buithutrang@gmail.com", 4, "Màu sắc tươi sáng, các nhân vật cảm xúc mới cực kỳ đáng yêu. Rất khuyên mọi người nên đi xem để ôm lấy những cảm xúc bên trong mình.", ReviewStatus.Published, 5),

                // 13. Lật Mặt 7: Một Điều Ước
                ("Lật Mặt 7: Một Điều Ước", "nguyenvana@gmail.com", 5, "Phim của anh Lý Hải lần này chạm đến tận cùng cảm xúc tình mẫu tử. Câu chuyện bà Hai và những người con khiến cả rạp nghẹn ngào, khóc nức nở.", ReviewStatus.Published, 26),
                ("Lật Mặt 7: Một Điều Ước", "tranthib@gmail.com", 5, "Diễn xuất của má Thanh Hiền quá chân thật, mộc mạc và lay động lòng người. Phim gia đình Việt Nam hay nhất năm nay!", ReviewStatus.Published, 19),
                ("Lật Mặt 7: Một Điều Ước", "doquanghuy@gmail.com", 5, "Xem xong chỉ muốn gọi điện ngay về cho mẹ. Thông điệp hiếu thảo thiêng liêng và ý nghĩa vô cùng.", ReviewStatus.Published, 9),

                // 14. Godzilla x Kong: Đế Chế Mới
                ("Godzilla x Kong: Đế Chế Mới", "levanc@gmail.com", 5, "Quái thú choảng nhau từ lòng đất lên mặt đất rung chuyển cả rạp! Kỹ xảo CGI quá khủng, Kong đeo găng tay điện tử đấm cực kỳ uy lực.", ReviewStatus.Published, 17),
                ("Godzilla x Kong: Đế Chế Mới", "hoange@gmail.com", 4, "Hành động liên tục không ngơi nghỉ, kỹ xảo hoành tráng xem cực kỳ giải trí và đã mắt. Âm thanh rạp đập rung sàn.", ReviewStatus.Published, 8),

                // 15. Twisters
                ("Twisters", "doquanghuy@gmail.com", 4, "Kỹ xảo tái hiện bão xoáy kinh hoàng chân thực đến rợn người! Âm thanh rít gầm của gió lốc làm thót tim. Trải nghiệm xem rạp cực kỳ hồi hộp.", ReviewStatus.Published, 13),
                ("Twisters", "nguyenphuctien@gmail.com", 4, "Phim kịch tính nghẹt thở, diễn viên trẻ đẹp diễn xuất tự nhiên. Một bộ phim thảm họa thiên nhiên rất đáng xem ngoài rạp.", ReviewStatus.Published, 6)
            };

            foreach (var item in richReviewSeeds)
            {
                var movie = allMoviesForReviews.FirstOrDefault(m => m.Title.Equals(item.MovieTitle, StringComparison.OrdinalIgnoreCase));
                if (movie == null) continue;

                if (!userDict.TryGetValue(item.UserEmail.ToLowerInvariant(), out var user))
                    continue;

                bool exists = await context.Reviews.AnyAsync(r => r.UserId == user.Id && r.MovieId == movie.MovieId);
                if (exists) continue;

                context.Reviews.Add(new Review
                {
                    MovieId = movie.MovieId,
                    UserId = user.Id,
                    Rating = item.Rating,
                    Comment = item.Comment,
                    Status = item.Status,
                    CreatedAt = DateTime.Now.AddDays(-item.DaysAgo)
                });
            }
            await context.SaveChangesAsync();

            // Update movie average ratings based on published reviews
            var moviesToRate = await context.Movies.Include(m => m.Reviews).ToListAsync();
            foreach (var movie in moviesToRate)
            {
                var pubReviews = movie.Reviews.Where(r => r.Status == ReviewStatus.Published).ToList();
                if (pubReviews.Any())
                {
                    movie.Rating = Math.Round((decimal)pubReviews.Average(r => r.Rating), 1);
                }
            }
            await context.SaveChangesAsync();
        }
        // ── 8. Tự động sinh Suất chiếu Demo liên tục từ hôm nay và các đơn mẫu Test Quầy ──
        await EnsureUpcomingDemoShowtimesAndTestBookingsAsync(context);
    }

    /// <summary>
    /// Đảm bảo luôn có đầy đủ suất chiếu demo cho hôm nay và 5 ngày tới,
    /// đồng thời chuẩn bị sẵn các đơn hàng mẫu để kiểm thử Quầy POS / Quét QR / In vé nhiệt.
    /// </summary>
    public static async Task EnsureUpcomingDemoShowtimesAndTestBookingsAsync(ApplicationDbContext context)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;

        // 1. Kiểm tra số lượng suất chiếu sắp tới (từ hôm nay trở đi)
        int upcomingShowtimesCount = await context.Showtimes
            .CountAsync(s => s.StartTime >= now && s.Status == ShowtimeStatus.Available);

        var movies = await context.Movies.Where(m => m.IsActive).OrderBy(m => m.MovieId).ToListAsync();
        var rooms = await context.ScreeningRooms.Include(r => r.Cinema).Where(r => r.IsActive).OrderBy(r => r.RoomId).ToListAsync();

        if (movies.Any() && rooms.Any() && upcomingShowtimesCount < 40)
        {
            var prices = new Dictionary<int, decimal>();
            foreach (var rm in rooms)
            {
                if (rm.RoomName.Contains("Standard", StringComparison.OrdinalIgnoreCase)) prices[rm.RoomId] = 90000m;
                else if (rm.RoomName.Contains("VIP", StringComparison.OrdinalIgnoreCase)) prices[rm.RoomId] = 130000m;
                else if (rm.RoomName.Contains("IMAX", StringComparison.OrdinalIgnoreCase)) prices[rm.RoomId] = 180000m;
                else prices[rm.RoomId] = 100000m;
            }

            var newShowtimes = new List<Showtime>();
            int movieIdx = 0;

            // Lập lịch từ Hôm nay (ngày 0) đến 5 ngày tiếp theo
            for (int dayOffset = 0; dayOffset <= 5; dayOffset++)
            {
                var curDate = today.AddDays(dayOffset);

                // Khung giờ chiếu trong ngày
                double[] timeSlots;
                if (dayOffset == 0)
                {
                    // Hôm nay: Các suất chiếu thuận tiện từ trưa/chiều/tối
                    timeSlots = new[] { 10.75, 11.5, 13.5, 15.25, 17.0, 18.75, 20.5, 22.25 };
                }
                else
                {
                    // Các ngày tới: Các khung giờ chuẩn sáng/trưa/chiều/tối
                    timeSlots = new[] { 9.5, 11.5, 13.5, 15.5, 17.5, 19.5, 21.5 };
                }

                foreach (var room in rooms)
                {
                    foreach (var hour in timeSlots)
                    {
                        var startTime = curDate.AddHours(hour);
                        if (startTime < now.AddMinutes(15)) continue; // Bỏ qua nếu đã qua

                        // Kiểm tra xem phòng này đã có suất chiếu gần giờ này chưa
                        bool slotTaken = await context.Showtimes.AnyAsync(s => 
                            s.RoomId == room.RoomId && 
                            s.StartTime >= startTime.AddMinutes(-45) && 
                            s.StartTime <= startTime.AddMinutes(45));

                        if (slotTaken) continue;

                        var movie = movies[movieIdx % movies.Count];
                        movieIdx++;

                        newShowtimes.Add(new Showtime
                        {
                            MovieId = movie.MovieId,
                            RoomId = room.RoomId,
                            StartTime = startTime,
                            EndTime = startTime.AddMinutes(movie.Duration + 15),
                            TicketPrice = prices.GetValueOrDefault(room.RoomId, 90000m),
                            Format = room.RoomName.Contains("IMAX", StringComparison.OrdinalIgnoreCase) ? "IMAX" : (movie.Format ?? "2D"),
                            SubtitleType = movie.SubtitleType ?? "Phụ đề Việt",
                            Status = ShowtimeStatus.Available,
                            Notes = $"Suất chiếu {movie.Title} tại {room.Cinema?.Name ?? room.RoomName}"
                        });
                    }
                }
            }

            if (newShowtimes.Any())
            {
                context.Showtimes.AddRange(newShowtimes);
                await context.SaveChangesAsync();

                // Sinh ShowtimeSeats cho toàn bộ suất chiếu mới
                var allActiveSeats = await context.Seats
                    .Where(s => s.IsActive)
                    .Include(s => s.SeatType)
                    .ToListAsync();
                var seatsByRoom = allActiveSeats.GroupBy(s => s.RoomId).ToDictionary(g => g.Key, g => g.ToList());

                var newSeatsList = new List<ShowtimeSeat>();
                foreach (var st in newShowtimes)
                {
                    if (seatsByRoom.TryGetValue(st.RoomId, out var rSeats))
                    {
                        foreach (var seat in rSeats)
                        {
                            decimal surcharge = seat.SeatType?.Surcharge ?? 
                                (seat.SeatTypeLegacy == SeatTypeEnum.VIP ? 15000m : seat.SeatTypeLegacy == SeatTypeEnum.Couple ? 40000m : 0m);

                            newSeatsList.Add(new ShowtimeSeat
                            {
                                ShowtimeId = st.ShowtimeId,
                                SeatId = seat.SeatId,
                                SeatTypeId = seat.SeatTypeId,
                                Price = st.TicketPrice + surcharge,
                                Status = ShowtimeSeatStatus.Available,
                                Version = 1,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                if (newSeatsList.Any())
                {
                    context.ShowtimeSeats.AddRange(newSeatsList);
                    await context.SaveChangesAsync();
                }
            }
        }

        // 2. Tạo sẵn 3 đơn hàng mẫu đặc biệt để test ngay trên Quầy POS (/Counter):
        var staffUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "staff@cinema.com");
        var customerUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "customer@cinema.com") ?? staffUser;
        var foodItems = await context.FoodItems.Where(f => f.IsAvailable).ToListAsync();

        var demoShowtime = await context.Showtimes
            .Include(s => s.Room)
            .Where(s => s.StartTime >= now && s.Status == ShowtimeStatus.Available)
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (demoShowtime != null && customerUser != null)
        {
            var stSeats = await context.ShowtimeSeats
                .Include(ss => ss.Seat)
                .Where(ss => ss.ShowtimeId == demoShowtime.ShowtimeId && ss.Status == ShowtimeSeatStatus.Available)
                .OrderBy(ss => ss.SeatId)
                .Take(6)
                .ToListAsync();

            // Mẫu 1: BK-DEMO-ONLINE-01 (Đã thanh toán Online, Chưa in vé, có sẵn Combo Bắp nước)
            if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-DEMO-ONLINE-01") && stSeats.Count >= 2)
            {
                var seat1 = stSeats[0];
                var seat2 = stSeats[1];
                seat1.Status = ShowtimeSeatStatus.Reserved;
                seat2.Status = ShowtimeSeatStatus.Reserved;

                var bkg1 = new Booking
                {
                    BookingCode = "BK-DEMO-ONLINE-01",
                    UserId = customerUser.Id,
                    ShowtimeId = demoShowtime.ShowtimeId,
                    BookingTime = DateTime.Now.AddMinutes(-20),
                    TicketAmount = seat1.Price + seat2.Price,
                    FnBAmount = 109000,
                    TotalAmount = seat1.Price + seat2.Price + 109000,
                    FinalAmount = seat1.Price + seat2.Price + 109000,
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = PaymentMethod.BankCard,
                    PaymentReference = "VIETQR-DEMO-01",
                    PaidAt = DateTime.Now.AddMinutes(-18),
                    ContactEmail = customerUser.Email,
                    ContactPhone = "0901234567",
                    CreatedAt = DateTime.Now.AddMinutes(-20)
                };
                context.Bookings.Add(bkg1);
                await context.SaveChangesAsync();

                var tck1 = new Ticket
                {
                    BookingId = bkg1.BookingId,
                    ShowtimeId = demoShowtime.ShowtimeId,
                    SeatId = seat1.SeatId,
                    ShowtimeSeatId = seat1.ShowtimeSeatId,
                    TicketCode = "TCK-DEMO-01-A",
                    SeatCode = $"{seat1.Seat?.RowLabel}{seat1.Seat?.SeatNumber}",
                    UnitPrice = seat1.Price,
                    Status = TicketStatus.Issued,
                    QrPayload = $"cinema-ticket:{bkg1.BookingId}:TCK-DEMO-01-A",
                    IsPrinted = false,
                    CreatedAt = DateTime.Now.AddMinutes(-20)
                };
                var tck2 = new Ticket
                {
                    BookingId = bkg1.BookingId,
                    ShowtimeId = demoShowtime.ShowtimeId,
                    SeatId = seat2.SeatId,
                    ShowtimeSeatId = seat2.ShowtimeSeatId,
                    TicketCode = "TCK-DEMO-01-B",
                    SeatCode = $"{seat2.Seat?.RowLabel}{seat2.Seat?.SeatNumber}",
                    UnitPrice = seat2.Price,
                    Status = TicketStatus.Issued,
                    QrPayload = $"cinema-ticket:{bkg1.BookingId}:TCK-DEMO-01-B",
                    IsPrinted = false,
                    CreatedAt = DateTime.Now.AddMinutes(-20)
                };
                context.Tickets.AddRange(tck1, tck2);

                if (foodItems.Any())
                {
                    var fnb = foodItems.First();
                    context.BookingConcessions.Add(new BookingConcession
                    {
                        BookingId = bkg1.BookingId,
                        FoodItemId = fnb.FoodItemId,
                        Quantity = 1,
                        UnitPrice = fnb.Price,
                        ItemName = fnb.Name
                    });
                }
                await context.SaveChangesAsync();
            }

            // Mẫu 2: BK-DEMO-UPSELL-02 (Đã thanh toán Online, Chưa in vé, CHƯA có bắp nước -> Để Staff mời mua thêm bắp nước)
            if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-DEMO-UPSELL-02") && stSeats.Count >= 4)
            {
                var seat3 = stSeats[2];
                var seat4 = stSeats[3];
                seat3.Status = ShowtimeSeatStatus.Reserved;
                seat4.Status = ShowtimeSeatStatus.Reserved;

                var bkg2 = new Booking
                {
                    BookingCode = "BK-DEMO-UPSELL-02",
                    UserId = customerUser.Id,
                    ShowtimeId = demoShowtime.ShowtimeId,
                    BookingTime = DateTime.Now.AddMinutes(-15),
                    TicketAmount = seat3.Price + seat4.Price,
                    FnBAmount = 0,
                    TotalAmount = seat3.Price + seat4.Price,
                    FinalAmount = seat3.Price + seat4.Price,
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = PaymentMethod.BankCard,
                    PaymentReference = "VIETQR-DEMO-02",
                    PaidAt = DateTime.Now.AddMinutes(-14),
                    ContactEmail = customerUser.Email,
                    ContactPhone = "0987654321",
                    CreatedAt = DateTime.Now.AddMinutes(-15)
                };
                context.Bookings.Add(bkg2);
                await context.SaveChangesAsync();

                context.Tickets.AddRange(
                    new Ticket
                    {
                        BookingId = bkg2.BookingId,
                        ShowtimeId = demoShowtime.ShowtimeId,
                        SeatId = seat3.SeatId,
                        ShowtimeSeatId = seat3.ShowtimeSeatId,
                        TicketCode = "TCK-DEMO-02-A",
                        SeatCode = $"{seat3.Seat?.RowLabel}{seat3.Seat?.SeatNumber}",
                        UnitPrice = seat3.Price,
                        Status = TicketStatus.Issued,
                        QrPayload = $"cinema-ticket:{bkg2.BookingId}:TCK-DEMO-02-A",
                        IsPrinted = false,
                        CreatedAt = DateTime.Now.AddMinutes(-15)
                    },
                    new Ticket
                    {
                        BookingId = bkg2.BookingId,
                        ShowtimeId = demoShowtime.ShowtimeId,
                        SeatId = seat4.SeatId,
                        ShowtimeSeatId = seat4.ShowtimeSeatId,
                        TicketCode = "TCK-DEMO-02-B",
                        SeatCode = $"{seat4.Seat?.RowLabel}{seat4.Seat?.SeatNumber}",
                        UnitPrice = seat4.Price,
                        Status = TicketStatus.Issued,
                        QrPayload = $"cinema-ticket:{bkg2.BookingId}:TCK-DEMO-02-B",
                        IsPrinted = false,
                        CreatedAt = DateTime.Now.AddMinutes(-15)
                    }
                );
                await context.SaveChangesAsync();
            }

            // Mẫu 3: BK-DEMO-PRINTED-03 (Vé ĐÃ IN VẬT LÝ trước đó -> Quét vào sẽ BẬT CHUÔNG BÁO ĐỎ TỪ CHỐI IN LẠI)
            if (!await context.Bookings.AnyAsync(b => b.BookingCode == "BK-DEMO-PRINTED-03") && stSeats.Count >= 6)
            {
                var seat5 = stSeats[4];
                var seat6 = stSeats[5];
                seat5.Status = ShowtimeSeatStatus.Reserved;
                seat6.Status = ShowtimeSeatStatus.Reserved;

                var bkg3 = new Booking
                {
                    BookingCode = "BK-DEMO-PRINTED-03",
                    UserId = customerUser.Id,
                    ShowtimeId = demoShowtime.ShowtimeId,
                    BookingTime = DateTime.Now.AddHours(-1),
                    TicketAmount = seat5.Price + seat6.Price,
                    FnBAmount = 85000,
                    TotalAmount = seat5.Price + seat6.Price + 85000,
                    FinalAmount = seat5.Price + seat6.Price + 85000,
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentReference = "CASH-DEMO-03",
                    PaidAt = DateTime.Now.AddMinutes(-45),
                    ContactEmail = customerUser.Email,
                    ContactPhone = "0911223344",
                    CreatedAt = DateTime.Now.AddHours(-1)
                };
                context.Bookings.Add(bkg3);
                await context.SaveChangesAsync();

                context.Tickets.AddRange(
                    new Ticket
                    {
                        BookingId = bkg3.BookingId,
                        ShowtimeId = demoShowtime.ShowtimeId,
                        SeatId = seat5.SeatId,
                        ShowtimeSeatId = seat5.ShowtimeSeatId,
                        TicketCode = "TCK-DEMO-03-A",
                        SeatCode = $"{seat5.Seat?.RowLabel}{seat5.Seat?.SeatNumber}",
                        UnitPrice = seat5.Price,
                        Status = TicketStatus.CheckedIn,
                        QrPayload = $"cinema-ticket:{bkg3.BookingId}:TCK-DEMO-03-A",
                        IsPrinted = true,
                        PrintedAt = DateTime.Now.AddMinutes(-30),
                        PrintedByUserId = staffUser?.Id ?? customerUser.Id,
                        ThermalReceiptCode = "POS80-DEMO-PRINTED-99",
                        CreatedAt = DateTime.Now.AddHours(-1)
                    },
                    new Ticket
                    {
                        BookingId = bkg3.BookingId,
                        ShowtimeId = demoShowtime.ShowtimeId,
                        SeatId = seat6.SeatId,
                        ShowtimeSeatId = seat6.ShowtimeSeatId,
                        TicketCode = "TCK-DEMO-03-B",
                        SeatCode = $"{seat6.Seat?.RowLabel}{seat6.Seat?.SeatNumber}",
                        UnitPrice = seat6.Price,
                        Status = TicketStatus.CheckedIn,
                        QrPayload = $"cinema-ticket:{bkg3.BookingId}:TCK-DEMO-03-B",
                        IsPrinted = true,
                        PrintedAt = DateTime.Now.AddMinutes(-30),
                        PrintedByUserId = staffUser?.Id ?? customerUser.Id,
                        ThermalReceiptCode = "POS80-DEMO-PRINTED-99",
                        CreatedAt = DateTime.Now.AddHours(-1)
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
