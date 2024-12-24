using Bot.App.Interfaces;
using Bot.App.Services;
using Bot.Data.Storages;
using Bot.Domain.Entities;

namespace Bot.App.Tests
{
    public class UserServiceTests
    {
       // private readonly IUserService _userService;
       // private readonly IUserStorage _userStorage;

        [Fact]
        public async void Test1()
        {
            // Arrange
            IUserStorage storage = new UserStorage(new Data.BotDbContext());
            var userService = new UserService(storage);
            //  var testDataGenerator = new TestDataGenerator();
            //   var clients = testDataGenerator.GenerateClients(10);

            //// Act
            //foreach (var user in users)
            //{
            //    userService.AddUserAsync(user);
            //}

            //User expectedUser = users[0];

            var user = new User
            {

                ChatId = new long(),

                TelegramNickname = "@asef",

                FirstName = "Петя",

                LastName = "Gtaw",


                //PhoneNumber ="7777777",

                Created = DateTime.Now.ToUniversalTime(),

                Currency = "Рубль ПМР"
            };

            await userService.AddUserAsync(user);

            // Assert
            Assert.Equal(user, await userService.GetUserAsync(user.Id));
        }
    }
}