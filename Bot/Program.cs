using Bot.App.Interfaces;
using Bot.App.Services;
using Bot.Data.Storages;
using Bot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Telegram.Bot.TelegramBotClient;

namespace Bot;

class Program
{
    private static ITelegramBotClient _botClient;

    private static ReceiverOptions _receiverOptions;

    private static ConcurrentDictionary<long, string> UserStatesTransactionType = new ConcurrentDictionary<long, string>();
    private static ConcurrentDictionary<long, Guid> UsersSetDescription= new ConcurrentDictionary<long, Guid>();
    private static ConcurrentDictionary<long, Guid> UsersUpdateCategory = new ConcurrentDictionary<long, Guid>();
    private static ConcurrentDictionary<long, Guid> UsersUpdateSum = new ConcurrentDictionary<long, Guid>();
    private static ConcurrentDictionary<long, Guid> UsersUpdateDescription = new ConcurrentDictionary<long, Guid>();

    public static async Task Main()
    {
        _botClient = new TelegramBotClient("7547971925:AAHbdzKemwmbtuGbEFelqXwMyLWuBg1a4NA");
        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message, 
                UpdateType.CallbackQuery  
            },
        };

        using var cts = new CancellationTokenSource();
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token); 

        var user = await _botClient.GetMeAsync();
        Console.WriteLine($"{user.Username} запущен!");

        await Task.Delay(-1); 
    }

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken token)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
   

    [Obsolete]
    private static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken token)
    {
        try
        {
            IUserStorage userStorage = new UserStorage(new Data.BotDbContext());
            ITransactionStorage transactionStorage = new TransactionStorage(new Data.BotDbContext());
            var userService = new UserService(userStorage);
            var transactionService = new TransactionService(transactionStorage);

            switch (update.Type)
            {
                case UpdateType.Message:
                    {
                        var message = update.Message;

                        var user = message.From;
                        Console.WriteLine($"{user.Username} ({user.Id}) написал сообщение: {message.Text}");

                        var chat = message.Chat;

                        switch (message.Type)
                        {
                            case MessageType.Text:
                                {
                                    var textMessage = message.Text;

                                    if (textMessage == "/start")
                                    {
                                        var expectedUser = await userService.GetUserAsync(user.Id);

                                        if (expectedUser.Id != user.Id && expectedUser.ChatId != message.Chat.Id)
                                        {

                                            var newUser = new Domain.Entities.User
                                            {
                                                Id = user.Id,

                                                ChatId = message.Chat.Id,

                                                TelegramNickname = user.Username,

                                                FirstName = user.FirstName,

                                                LastName = user.LastName,

                                                Currency = "Рубль ПМР"
                                            };
                                            await userService.AddUserAsync(newUser);
                                                                                        
                                            var inlineKeyboard = new InlineKeyboardMarkup(
                                                new List<InlineKeyboardButton[]>() 
                                                {
                                                    new InlineKeyboardButton[] 
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Рубль ПМР", "RUP"),
                                                        InlineKeyboardButton.WithCallbackData("Доллар США", "USD"),
                                                    },
                                                    new InlineKeyboardButton[]
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Рубль РФ", "RUB"),
                                                        InlineKeyboardButton.WithCallbackData("Лей Молдова", "MLD"),
                                                    },
                                                });
                                            await botClient.SendTextMessageAsync(
                                                    chat.Id,
                                                    "Выберите валюту",
                                                    replyMarkup: inlineKeyboard); 

                                            return;

                                        }
                                        Console.WriteLine($"{user.Username} Уже зарегистрирован");

                                        return;
                                    }

                                    Console.WriteLine($"Пользователь ввел : {textMessage}");

                                    if (UsersSetDescription.TryGetValue(user.Id, out var transactionId))
                                    {
                                        var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                        transaction.Description = textMessage;

                                        var userInDb = await userService.GetUserAsync(user.Id);
                                                                          
                                        var messageId = await SendMessageWithTrackingAsync(botClient, chat.Id, $"{transaction.Type} составил {transaction.Amount} {userInDb.Currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transaction.Id);

                                     
                                        transaction.AmountMessageId = messageId;

                                        await transactionService.UpdateTransactionAsync(transaction.Id, transaction);

                                        if (transaction.Type == TransactionType.Расход)
                                        {
                                            decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                            if (userBalance<0)
                                            {
                                                await botClient.SendTextMessageAsync(
                                                  chat.Id,
                                                  "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                            }
                                        }

                                        UsersSetDescription.Remove(user.Id, out transactionId);

                                        return;
                                    }

                                    if (UserStatesTransactionType.TryGetValue(user.Id, out var action))
                                    {
                                        decimal amount = IsValidMoney(textMessage);

                                        if (amount > 0)
                                        {
                                            var newTransaction = new Domain.Entities.Transaction
                                            {
                                                UserId = user.Id,

                                                Amount = amount

                                            };

                                            if (action == "Доход")
                                            {
                                                newTransaction.Type = TransactionType.Доход;

                                                await transactionService.AddTransactionAsync(newTransaction);

                                                var idTransaction = newTransaction.Id;

                                                var inlineKeyboard = new InlineKeyboardMarkup(
                                                            new List<InlineKeyboardButton[]>()
                                                            {
                                                                new InlineKeyboardButton[]
                                                                {
                                                                    InlineKeyboardButton.WithCallbackData("Зарплата", $"Зарплата{idTransaction}"),
                                                                    InlineKeyboardButton.WithCallbackData("Фриланс и подработки", $"Фриланс{idTransaction}"),
                                                                },
                                                                   new InlineKeyboardButton[]
                                                                {
                                                                    InlineKeyboardButton.WithCallbackData("Долг", $"Долг{idTransaction}"),
                                                                    InlineKeyboardButton.WithCallbackData("Подарки и выигрыши", $"Подарки{idTransaction}"),
                                                                },
                                                                  new InlineKeyboardButton[]
                                                                {
                                                                    InlineKeyboardButton.WithCallbackData("Прочие доходы", $"Прочие+{idTransaction}"),
                                                                },
                                                            });

                                                await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Выберите категорию дохода",
                                                       replyMarkup: inlineKeyboard);
                                            }
                                            else
                                            {
                                                newTransaction.Type = TransactionType.Расход;

                                                await transactionService.AddTransactionAsync(newTransaction);

                                                var idTransaction = newTransaction.Id;

                                                var inlineKeyboard = new InlineKeyboardMarkup(
                                                           new List<InlineKeyboardButton[]>()
                                                           {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Продукты питания", $"Продукты{idTransaction}"),
                                                                InlineKeyboardButton.WithCallbackData("Развлечения и отдых", $"Развлечения{idTransaction}"),
                                                            },
                                                               new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Оплата жилья", $"Жилье{idTransaction}"),
                                                                InlineKeyboardButton.WithCallbackData("Транспорт", $"Транспорт{idTransaction}"),
                                                            },
                                                                 new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Шоппинг", $"Шоппинг{idTransaction}"),
                                                                InlineKeyboardButton.WithCallbackData("Долг", $"Долг{idTransaction}"),
                                                            },
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Прочие расходы", $"Прочие-{idTransaction}"),
                                                            },
                                                           });

                                                await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Выберите категорию расхода",
                                                       replyMarkup: inlineKeyboard);
                                            }



                                            Console.WriteLine($"{action}: {textMessage} добавлен пользователю: {user.FirstName} id: {newTransaction.Id}");

                                            UserStatesTransactionType.Remove(user.Id, out action);
                                            return;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"{textMessage} не является денежной суммой.");

                                            await botClient.SendTextMessageAsync(
                                                           chat.Id,
                                                           $"{textMessage} не является денежной суммой.");
                                        }
                                       
                                        return;
                                    }

                                    if (UsersUpdateSum.TryGetValue(user.Id, out transactionId))
                                    {
                                        decimal amount = IsValidMoney(textMessage);
                                        if (amount > 0)
                                        {
                                            var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                            transaction.Amount = amount;
                                            await transactionService.UpdateTransactionAsync(transactionId, transaction);
                                            var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                            
                                            await EditMessageWithTrackingAsync(botClient, message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);

                                            await botClient.SendTextMessageAsync(
                                                      chat.Id,
                                                      "Данные успешно изменены");

                                            UsersUpdateSum.Remove(user.Id, out transactionId);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"{textMessage} не является денежной суммой.");

                                            await botClient.SendTextMessageAsync(
                                                           chat.Id,
                                                           $"{textMessage} не является денежной суммой.");
                                        }

                                        return;
                                    }

                                    if (UsersUpdateDescription.TryGetValue(user.Id, out transactionId))
                                    {
                                        var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                        transaction.Description = textMessage;
                                        await transactionService.UpdateTransactionAsync(transactionId, transaction);
                                        var currency = (await userService.GetUserAsync(user.Id)).Currency;

                                        await EditMessageWithTrackingAsync(botClient, message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);

                                        await botClient.SendTextMessageAsync(
                                                  chat.Id,
                                                  "Данные успешно изменены");

                                        if (transaction.Type == TransactionType.Расход)
                                        {
                                            decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                            if (userBalance < 0)
                                            {
                                                await botClient.SendTextMessageAsync(
                                                  chat.Id,
                                                  "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                            }
                                        }

                                        UsersUpdateDescription.Remove(user.Id, out transactionId);
                                        return;
                                    }

                                    return;
                                }
                        }
                        return;
                    }

                case UpdateType.CallbackQuery:
                    {
                        var callbackQuery = update.CallbackQuery;

                        var user = callbackQuery.From;

                        Console.WriteLine($"{user.Username} ({user.Id}) нажал на кнопку: {callbackQuery.Data}");
                        
                        var chat = callbackQuery.Message.Chat;

                        switch (callbackQuery.Data)
                        {
                            case "RUP":
                                {
                                    await ChangeCurrecyAsync(botClient, callbackQuery, userService, user, chat);
                                    await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                                    return;
                                }

                            case "USD":
                                {
                                    await ChangeCurrecyAsync(botClient, callbackQuery, userService, user, chat);
                                    await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                                    return;
                                }

                            case "RUB":
                                {
                                    await ChangeCurrecyAsync(botClient, callbackQuery, userService, user, chat);
                                    await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                                    return;
                                }

                            case "MLD":
                                {
                                    await ChangeCurrecyAsync(botClient, callbackQuery, userService, user, chat);
                                    await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                                    return;
                                }

                            case "Cancel":
                                {
                                    await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                    return;
                                }

                            case "Записать доход":
                                {
                                    await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                    UserStatesTransactionType[user.Id] = "Доход";

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                                     chat.Id,
                                                     "Введите сумму дохода");
                                 
                                    return;
                                }

                            case "Записать расход":
                                {
                                    await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                    UserStatesTransactionType[user.Id] = "Расход";

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                            chat.Id,
                                            "Введите сумму расхода");

                                    return;
                                }

                            case "Новая операция":
                                {
                                    await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                                    return;
                                }

                            case "Посмотреть статистику":
                                {
                                    var inlineKeyboard = new InlineKeyboardMarkup(
                                                             new List<InlineKeyboardButton[]>()
                                                             {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Сегодня", "Сегодня"),
                                                                InlineKeyboardButton.WithCallbackData("Неделя", "Неделя"),
                                                            },
                                                            new InlineKeyboardButton[]
                                                            {
                                                               InlineKeyboardButton.WithCallbackData("Месяц", "Месяц"),
                                                               InlineKeyboardButton.WithCallbackData("Всё время", "Всё время"),
                                                            },
                                                             });

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                           chat.Id,
                                           "Выберите промежуток времени",
                                           replyMarkup: inlineKeyboard);

                                    return;
                                }

                            case "Сегодня":
                                {
                                    await botClient.DeleteMessageAsync(
                                     chatId: callbackQuery.Message.Chat.Id,
                                     messageId: callbackQuery.Message.MessageId);

                                    var todayStart = DateTime.UtcNow.Date;
                                    var todayEnd = todayStart.AddDays(1);

                                    var todayData = await transactionService.GetTransactionsByUserIdInRangeAsync(user.Id, todayStart, todayEnd);

                                    string incomeCategoryText = "";
                                    string expenseCategoryText = "";
                                    decimal income = 0, expense = 0, selary = 0, freelance = 0, dutyIncomes = 0, gifts = 0, otherIncomes = 0, otherExpenses = 0, shopping = 0, dutyExpenses = 0, transport = 0, homeBilling = 0, chill = 0, food = 0;
                                    ConcurrentDictionary<string,decimal> incomes = new ConcurrentDictionary<string, decimal>();
                                    ConcurrentDictionary<string, decimal> expenses = new ConcurrentDictionary<string, decimal>();
                                    string border = "\n➖➖➖➖➖➖➖➖➖➖";
                                    string border1 = "";
                                    string border2 = "";
                                    foreach (var t in todayData) 
                                    {
                                        if (t.Type == 0)
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие доходы":
                                                    otherIncomes += t.Amount;
                                                    incomes["Прочие доходы"] = otherIncomes;
                                                    break;
                                                case "Подарки и выигрыши":
                                                    gifts += t.Amount;
                                                    incomes["Подарки и выигрыши"] = gifts;
                                                    break;
                                                case "Долг":
                                                    dutyIncomes += t.Amount;
                                                    incomes["Долг"] = dutyIncomes;
                                                    break;
                                                case "Фриланс и подработки":
                                                    freelance += t.Amount;
                                                    incomes["Фриланс и подработки"] = freelance;
                                                    break;
                                                case "Зарплата":
                                                    selary += t.Amount;
                                                    incomes["Зарплата"] = selary;
                                                    break; 
                                            }
                                            income += t.Amount;

                                        }
                                        else
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие расходы":
                                                    otherExpenses += t.Amount;
                                                    expenses["Прочие расходы"] = otherExpenses;
                                                    break;
                                                case "Шоппинг":
                                                    shopping += t.Amount;
                                                    expenses["Шоппинг"] = shopping;
                                                    break;
                                                case "Долг":
                                                    dutyExpenses += t.Amount;
                                                    expenses["Долг"] = dutyExpenses;
                                                    break;
                                                case "Транспорт":
                                                    transport += t.Amount;
                                                    expenses["Транспорт"] = transport;
                                                    break;
                                                case "Оплата жилья":
                                                    homeBilling += t.Amount;
                                                    expenses["Оплата жилья"] = homeBilling;
                                                    break;
                                                case "Развлечения и отдых":
                                                    chill += t.Amount;
                                                    expenses["Развлечения и отдых"] = chill;
                                                    break;
                                                case "Продукты питания":
                                                    food += t.Amount;
                                                    expenses["Продукты питания"] = food;
                                                    break;
                                            }
                                            expense += t.Amount;
                                        }
                                    }

                                    if (incomes.Count > 0) 
                                    {
                                        border1 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }
                                    if (expenses.Count > 0) 
                                    {
                                        border2 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }

                                    string currency = (await userService.GetUserAsync(user.Id)).Currency;

                                    foreach (var cat in incomes)
                                    {
                                        incomeCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    foreach (var cat in expenses)
                                    {
                                        expenseCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(
                                                                    new List<InlineKeyboardButton[]>()
                                                                    {
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                            },
                                                                new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                            },
                                                                    });

                                    var text = $"Доходы за сегодня: {income} {currency}{border}{incomeCategoryText}{border1}\nРасходы за сегодня: {expense} {currency}{border}{expenseCategoryText}{border2}";
                                                 
                                    var sendMessage = await botClient.SendTextMessageAsync(
                                           chat.Id,
                                           text,
                                           replyMarkup: inlineKeyboard);

                                    return;
                                }

                            case "Неделя":
                                {
                                    await botClient.DeleteMessageAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId);

                                    var Start = DateTime.UtcNow.Date.AddDays(-7);
                                    var End = DateTime.UtcNow.Date.AddDays(1);

                                    var todayData = await transactionService.GetTransactionsByUserIdInRangeAsync(user.Id, Start, End);

                                    string incomeCategoryText = "";
                                    string expenseCategoryText = "";
                                    decimal income = 0, expense = 0, selary = 0, freelance = 0, dutyIncomes = 0, gifts = 0, otherIncomes = 0, otherExpenses = 0, shopping = 0, dutyExpenses = 0, transport = 0, homeBilling = 0, chill = 0, food = 0;
                                    ConcurrentDictionary<string, decimal> incomes = new ConcurrentDictionary<string, decimal>();
                                    ConcurrentDictionary<string, decimal> expenses = new ConcurrentDictionary<string, decimal>();
                                    string border = "\n➖➖➖➖➖➖➖➖➖➖";
                                    string border1 = "";
                                    string border2 = "";
                                    foreach (var t in todayData)
                                    {
                                        if (t.Type == 0)
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие доходы":
                                                    otherIncomes += t.Amount;
                                                    incomes["Прочие доходы"] = otherIncomes;
                                                    break;
                                                case "Подарки и выигрыши":
                                                    gifts += t.Amount;
                                                    incomes["Подарки и выигрыши"] = gifts;
                                                    break;
                                                case "Долг":
                                                    dutyIncomes += t.Amount;
                                                    incomes["Долг"] = dutyIncomes;
                                                    break;
                                                case "Фриланс и подработки":
                                                    freelance += t.Amount;
                                                    incomes["Фриланс и подработки"] = freelance;
                                                    break;
                                                case "Зарплата":
                                                    selary += t.Amount;
                                                    incomes["Зарплата"] = selary;
                                                    break;
                                            }
                                            income += t.Amount;

                                        }
                                        else
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие расходы":
                                                    otherExpenses += t.Amount;
                                                    expenses["Прочие расходы"] = otherExpenses;
                                                    break;
                                                case "Шоппинг":
                                                    shopping += t.Amount;
                                                    expenses["Шоппинг"] = shopping;
                                                    break;
                                                case "Долг":
                                                    dutyExpenses += t.Amount;
                                                    expenses["Долг"] = dutyExpenses;
                                                    break;
                                                case "Транспорт":
                                                    transport += t.Amount;
                                                    expenses["Транспорт"] = transport;
                                                    break;
                                                case "Оплата жилья":
                                                    homeBilling += t.Amount;
                                                    expenses["Оплата жилья"] = homeBilling;
                                                    break;
                                                case "Развлечения и отдых":
                                                    chill += t.Amount;
                                                    expenses["Развлечения и отдых"] = chill;
                                                    break;
                                                case "Продукты питания":
                                                    food += t.Amount;
                                                    expenses["Продукты питания"] = food;
                                                    break;
                                            }
                                            expense += t.Amount;
                                        }
                                    }

                                    if (incomes.Count > 0)
                                    {
                                        border1 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }
                                    if (expenses.Count > 0)
                                    {
                                        border2 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }

                                    string currency = (await userService.GetUserAsync(user.Id)).Currency;

                                    foreach (var cat in incomes)
                                    {
                                        incomeCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    foreach (var cat in expenses)
                                    {
                                        expenseCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(
                                                                    new List<InlineKeyboardButton[]>()
                                                                    {
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                            },
                                                                new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                            },
                                                                    });

                                    var text = $"Доходы за неделю: {income} {currency}{border}{incomeCategoryText}{border1}\nРасходы за неделю: {expense} {currency}{border}{expenseCategoryText}{border2}";

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                           chat.Id,
                                           text,
                                           replyMarkup: inlineKeyboard);

                                    return;
                                }

                            case "Месяц":
                                {
                                    await botClient.DeleteMessageAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId);

                                    var Start = DateTime.UtcNow.Date.AddMonths(-1);
                                    var End = DateTime.UtcNow.Date.AddDays(1);

                                    var todayData = await transactionService.GetTransactionsByUserIdInRangeAsync(user.Id, Start, End);

                                    string incomeCategoryText = "";
                                    string expenseCategoryText = "";
                                    decimal income = 0, expense = 0, selary = 0, freelance = 0, dutyIncomes = 0, gifts = 0, otherIncomes = 0, otherExpenses = 0, shopping = 0, dutyExpenses = 0, transport = 0, homeBilling = 0, chill = 0, food = 0;
                                    ConcurrentDictionary<string, decimal> incomes = new ConcurrentDictionary<string, decimal>();
                                    ConcurrentDictionary<string, decimal> expenses = new ConcurrentDictionary<string, decimal>();
                                    string border = "\n➖➖➖➖➖➖➖➖➖➖";
                                    string border1 = "";
                                    string border2 = "";
                                    foreach (var t in todayData)
                                    {
                                        if (t.Type == 0)
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие доходы":
                                                    otherIncomes += t.Amount;
                                                    incomes["Прочие доходы"] = otherIncomes;
                                                    break;
                                                case "Подарки и выигрыши":
                                                    gifts += t.Amount;
                                                    incomes["Подарки и выигрыши"] = gifts;
                                                    break;
                                                case "Долг":
                                                    dutyIncomes += t.Amount;
                                                    incomes["Долг"] = dutyIncomes;
                                                    break;
                                                case "Фриланс и подработки":
                                                    freelance += t.Amount;
                                                    incomes["Фриланс и подработки"] = freelance;
                                                    break;
                                                case "Зарплата":
                                                    selary += t.Amount;
                                                    incomes["Зарплата"] = selary;
                                                    break;
                                            }
                                            income += t.Amount;

                                        }
                                        else
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие расходы":
                                                    otherExpenses += t.Amount;
                                                    expenses["Прочие расходы"] = otherExpenses;
                                                    break;
                                                case "Шоппинг":
                                                    shopping += t.Amount;
                                                    expenses["Шоппинг"] = shopping;
                                                    break;
                                                case "Долг":
                                                    dutyExpenses += t.Amount;
                                                    expenses["Долг"] = dutyExpenses;
                                                    break;
                                                case "Транспорт":
                                                    transport += t.Amount;
                                                    expenses["Транспорт"] = transport;
                                                    break;
                                                case "Оплата жилья":
                                                    homeBilling += t.Amount;
                                                    expenses["Оплата жилья"] = homeBilling;
                                                    break;
                                                case "Развлечения и отдых":
                                                    chill += t.Amount;
                                                    expenses["Развлечения и отдых"] = chill;
                                                    break;
                                                case "Продукты питания":
                                                    food += t.Amount;
                                                    expenses["Продукты питания"] = food;
                                                    break;
                                            }
                                            expense += t.Amount;
                                        }
                                    }

                                    if (incomes.Count > 0)
                                    {
                                        border1 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }
                                    if (expenses.Count > 0)
                                    {
                                        border2 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }

                                    string currency = (await userService.GetUserAsync(user.Id)).Currency;

                                    foreach (var cat in incomes)
                                    {
                                        incomeCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    foreach (var cat in expenses)
                                    {
                                        expenseCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(
                                                                    new List<InlineKeyboardButton[]>()
                                                                    {
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                            },
                                                                new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                            },
                                                                    });

                                    var text = $"Доходы за месяц: {income} {currency}{border}{incomeCategoryText}{border1}\nРасходы за месяц: {expense} {currency}{border}{expenseCategoryText}{border2}";

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                           chat.Id,
                                           text,
                                           replyMarkup: inlineKeyboard);

                                    return;
                                }

                            case "Всё время":
                                {
                                    await botClient.DeleteMessageAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    messageId: callbackQuery.Message.MessageId);

                                    

                                    var todayData = await transactionService.GetTransactionsByUserIdAsync(user.Id);

                                    string incomeCategoryText = "";
                                    string expenseCategoryText = "";
                                    decimal income = 0, expense = 0, selary = 0, freelance = 0, dutyIncomes = 0, gifts = 0, otherIncomes = 0, otherExpenses = 0, shopping = 0, dutyExpenses = 0, transport = 0, homeBilling = 0, chill = 0, food = 0;
                                    ConcurrentDictionary<string, decimal> incomes = new ConcurrentDictionary<string, decimal>();
                                    ConcurrentDictionary<string, decimal> expenses = new ConcurrentDictionary<string, decimal>();
                                    string border = "\n➖➖➖➖➖➖➖➖➖➖";
                                    string border1 = "";
                                    string border2 = "";
                                    foreach (var t in todayData)
                                    {
                                        if (t.Type == 0)
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие доходы":
                                                    otherIncomes += t.Amount;
                                                    incomes["Прочие доходы"] = otherIncomes;
                                                    break;
                                                case "Подарки и выигрыши":
                                                    gifts += t.Amount;
                                                    incomes["Подарки и выигрыши"] = gifts;
                                                    break;
                                                case "Долг":
                                                    dutyIncomes += t.Amount;
                                                    incomes["Долг"] = dutyIncomes;
                                                    break;
                                                case "Фриланс и подработки":
                                                    freelance += t.Amount;
                                                    incomes["Фриланс и подработки"] = freelance;
                                                    break;
                                                case "Зарплата":
                                                    selary += t.Amount;
                                                    incomes["Зарплата"] = selary;
                                                    break;
                                            }
                                            income += t.Amount;

                                        }
                                        else
                                        {
                                            switch (t.Category)
                                            {
                                                case "Прочие расходы":
                                                    otherExpenses += t.Amount;
                                                    expenses["Прочие расходы"] = otherExpenses;
                                                    break;
                                                case "Шоппинг":
                                                    shopping += t.Amount;
                                                    expenses["Шоппинг"] = shopping;
                                                    break;
                                                case "Долг":
                                                    dutyExpenses += t.Amount;
                                                    expenses["Долг"] = dutyExpenses;
                                                    break;
                                                case "Транспорт":
                                                    transport += t.Amount;
                                                    expenses["Транспорт"] = transport;
                                                    break;
                                                case "Оплата жилья":
                                                    homeBilling += t.Amount;
                                                    expenses["Оплата жилья"] = homeBilling;
                                                    break;
                                                case "Развлечения и отдых":
                                                    chill += t.Amount;
                                                    expenses["Развлечения и отдых"] = chill;
                                                    break;
                                                case "Продукты питания":
                                                    food += t.Amount;
                                                    expenses["Продукты питания"] = food;
                                                    break;
                                            }
                                            expense += t.Amount;
                                        }
                                    }

                                    if (incomes.Count > 0)
                                    {
                                        border1 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }
                                    if (expenses.Count > 0)
                                    {
                                        border2 = "\n➖➖➖➖➖➖➖➖➖➖";
                                    }

                                    string currency = (await userService.GetUserAsync(user.Id)).Currency;

                                    foreach (var cat in incomes)
                                    {
                                        incomeCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    foreach (var cat in expenses)
                                    {
                                        expenseCategoryText += $"\n ▶️{cat.Key}: {cat.Value} {currency}";
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(
                                                                    new List<InlineKeyboardButton[]>()
                                                                    {
                                                                          new InlineKeyboardButton[]
                                                                        {
                                                                            InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                                        },
                                                                            new InlineKeyboardButton[]
                                                                        {
                                                                            InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                                        },
                                                                    });

                                    var text = $"Доходы за всё время: {income} {currency}{border}{incomeCategoryText}{border1}\nРасходы за всё время: {expense} {currency}{border}{expenseCategoryText}{border2}";

                                    var sendMessage = await botClient.SendTextMessageAsync(
                                           chat.Id,
                                           text,
                                           replyMarkup: inlineKeyboard);

                                    return;
                                }

                        }

                        if (callbackQuery.Data.StartsWith("AddDescription"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("AddDescription", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                await botClient.SendTextMessageAsync(
                                                chatId: callbackQuery.Message.Chat.Id,
                                                text: "Введите описание");

                                UsersSetDescription[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("WithoutDescription"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("WithoutDescription", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Description = "";

                                var userInDb = await userService.GetUserAsync(user.Id);

                                

                                var messageId = await SendMessageWithTrackingAsync(botClient, chat.Id, $"{transaction.Type} составил {transaction.Amount} {userInDb.Currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transaction.Id);

                                transaction.AmountMessageId = messageId;

                                await transactionService.UpdateTransactionAsync(transaction.Id, transaction);

                                if (transaction.Type == TransactionType.Расход)
                                {
                                    decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                    if (userBalance < 0)
                                    {
                                        await botClient.SendTextMessageAsync(
                                          chat.Id,
                                          "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                    }
                                }

                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Зарплата"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Зарплата", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Зарплата";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");
                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Фриланс"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Фриланс", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Фриланс и подработки";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");
                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Долг"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Долг", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Долг";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Подарки"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Подарки", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Подарки и выигрыши";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");
                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Прочие+"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Прочие+", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Прочие доходы";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");
                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Продукты"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Продукты", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Продукты питания";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Развлечения"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Развлечения", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Развлечения и отдых";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Жилье"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Жилье", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Оплата жилья";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Транспорт"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Транспорт", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Транспорт";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Шоппинг"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Шоппинг", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Шоппинг";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Прочие-"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Прочие-", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Category = "Прочие расходы";
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                if (UsersUpdateCategory.TryGetValue(user.Id, out var updateTransactionId))
                                {
                                    var currency = (await userService.GetUserAsync(user.Id)).Currency;
                                    await EditMessageWithTrackingAsync(botClient, callbackQuery.Message.Chat.Id, (int)transaction.AmountMessageId, $"Изменено\n{transaction.Type} составил {transaction.Amount} {currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}", transactionId);
                                    await botClient.SendTextMessageAsync(
                                                       chat.Id,
                                                       "Данные успешно изменены");

                                    if (transaction.Type == TransactionType.Расход)
                                    {
                                        decimal userBalance = await transactionService.GetUserBalanceAsync(user.Id);
                                        if (userBalance < 0)
                                        {
                                            await botClient.SendTextMessageAsync(
                                              chat.Id,
                                              "Расходы привысили доходы. Возможно Вы забыли указать доход.");
                                        }
                                    }

                                    UsersUpdateCategory.Remove(user.Id, out updateTransactionId);
                                }
                                else
                                {
                                    await SendMessageWithSuggestAddingDescription(botClient, chat.Id, transactionId);
                                }
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("Редактировать"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Редактировать", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                var confirmKeyboard = new InlineKeyboardMarkup(
                                    new[]
                                    {
                                        new[]
                                        {
                                            InlineKeyboardButton.WithCallbackData($"Да, редактировать", $"ConfirmUpdate{transactionId}"),
                                            InlineKeyboardButton.WithCallbackData("Нет, отменить", "CancelUpdate")
                                        }
                                    });

                                var userInDb = await userService.GetUserAsync(user.Id);
                                await botClient.SendTextMessageAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    text: $"Точно ли хотите редактировать запись:\n{transaction.Type} {transaction.Amount} {userInDb.Currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}?",
                                    replyMarkup: confirmKeyboard);
                            }
                            return;
                        }
                        
                        if (callbackQuery.Data.StartsWith("Удалить"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("Удалить", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                var confirmKeyboard = new InlineKeyboardMarkup(
                                    new[]
                                    {
                                        new[]
                                        {
                                            InlineKeyboardButton.WithCallbackData($"Да, удалить", $"ConfirmDelete{transactionId}"),
                                            InlineKeyboardButton.WithCallbackData("Нет, отменить", "CancelDelete")
                                        }
                                    });

                                var userInDb = await userService.GetUserAsync(user.Id);
                                await botClient.SendTextMessageAsync(
                                    chatId: callbackQuery.Message.Chat.Id,
                                    text: $"Точно ли хотите удалить запись:\n{transaction.Type} {transaction.Amount} {userInDb.Currency}\nКатегория {transaction.Category}\nОписание {transaction.Description}?",
                                    replyMarkup: confirmKeyboard);
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("ConfirmUpdate"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("ConfirmUpdate", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);

                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var inlineKeyboard = new InlineKeyboardMarkup(
                                                          new List<InlineKeyboardButton[]>()
                                                          {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Тип", $"UpdateType{transactionId}"),
                                                            },
                                                            new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Сумму", $"UpdateSum{transactionId}"),
                                                            },
                                                              new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Категорию", $"UpdateCategory{transactionId}"),
                                                            },
                                                                new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Описание", $"UpdateDescription{transactionId}"),
                                                            },
                                                                     new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Отмена", $"Cancel"),
                                                            },
                                                          });
                                await botClient.SendTextMessageAsync(
                                        chat.Id,
                                        "Что изменим?",
                                        replyMarkup: inlineKeyboard);
                            }

                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateDescription"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateDescription", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var sendMessage = await botClient.SendTextMessageAsync(
                                     chat.Id,
                                     "Введите описание");

                                UsersUpdateDescription[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateSum"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateSum", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var sendMessage = await botClient.SendTextMessageAsync(
                                     chat.Id,
                                     "Введите сумму");

                                UsersUpdateSum[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateCategory"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateCategory", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                InlineKeyboardMarkup inlineKeyboard;

                                if (transaction.Type == TransactionType.Доход)
                                {
                                    inlineKeyboard = new InlineKeyboardMarkup(
                                               new List<InlineKeyboardButton[]>()
                                               {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Зарплата", $"Зарплата{transactionId}"),
                                                                InlineKeyboardButton.WithCallbackData("Фриланс и подработки", $"Фриланс{transactionId}"),
                                                            },
                                                               new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Долг", $"Долг{transactionId}"),
                                                                InlineKeyboardButton.WithCallbackData("Подарки и выигрыши", $"Подарки{transactionId}"),
                                                            },
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Прочие доходы", $"Прочие+{transactionId}"),
                                                            },
                                               });
                                }
                                else
                                {
                                    inlineKeyboard = new InlineKeyboardMarkup(
                                           new List<InlineKeyboardButton[]>()
                                           {
                                                    new InlineKeyboardButton[]
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Продукты питания", $"Продукты{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Развлечения и отдых", $"Развлечения{transactionId}"),
                                                    },
                                                       new InlineKeyboardButton[]
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Оплата жилья", $"Жилье{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Транспорт", $"Транспорт{transactionId}"),
                                                    },
                                                         new InlineKeyboardButton[]
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Шоппинг", $"Шоппинг{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Долг", $"Долг{transactionId}"),
                                                    },
                                                      new InlineKeyboardButton[]
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Прочие расходы", $"Прочие-{transactionId}"),
                                                    },
                                           });
                                }

                                await botClient.SendTextMessageAsync(
                                      chat.Id,
                                      "Выберите категорию дохода",
                                      replyMarkup: inlineKeyboard);

                                UsersUpdateCategory[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateType"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateType", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var inlineKeyboard = new InlineKeyboardMarkup(
                                             new List<InlineKeyboardButton[]>()
                                             {
                                                new InlineKeyboardButton[]
                                                {
                                                    InlineKeyboardButton.WithCallbackData("Доход🔼", $"UpdateIncome{transactionId}"),
                                                },
                                                new InlineKeyboardButton[]
                                                {
                                                     InlineKeyboardButton.WithCallbackData("Расход🔽", $"UpdateExpense{transactionId}"),
                                                },
                                             });
                                await botClient.SendTextMessageAsync(
                                        chat.Id,
                                        "Выберите тип",
                                        replyMarkup: inlineKeyboard);
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateIncome"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateIncome", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Type = TransactionType.Доход;
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                var inlineKeyboard = new InlineKeyboardMarkup(
                                            new List<InlineKeyboardButton[]>()
                                            {
                                                            new InlineKeyboardButton[] 
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Зарплата", $"Зарплата{transactionId}"),
                                                                InlineKeyboardButton.WithCallbackData("Фриланс и подработки", $"Фриланс{transactionId}"),
                                                            },
                                                               new InlineKeyboardButton[] 
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Долг", $"Долг{transactionId}"),
                                                                InlineKeyboardButton.WithCallbackData("Подарки и выигрыши", $"Подарки{transactionId}"),
                                                            },
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Прочие доходы", $"Прочие+{transactionId}"),
                                                            },
                                            });

                                var sendMessage = await botClient.SendTextMessageAsync(
                                       chat.Id,
                                       "Выберите категорию дохода",
                                       replyMarkup: inlineKeyboard);

                                UsersUpdateCategory[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("UpdateExpense"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("UpdateExpense", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                                transaction.Type = TransactionType.Расход;
                                await transactionService.UpdateTransactionAsync(transactionId, transaction);

                                var inlineKeyboard = new InlineKeyboardMarkup(
                                           new List<InlineKeyboardButton[]>()
                                           {
                                                    new InlineKeyboardButton[] 
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Продукты питания", $"Продукты{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Развлечения и отдых", $"Развлечения{transactionId}"),
                                                    },
                                                       new InlineKeyboardButton[] 
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Оплата жилья", $"Жилье{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Транспорт", $"Транспорт{transactionId}"),
                                                    },
                                                         new InlineKeyboardButton[] 
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Шоппинг", $"Шоппинг{transactionId}"),
                                                        InlineKeyboardButton.WithCallbackData("Долг", $"Долг{transactionId}"),
                                                    },
                                                      new InlineKeyboardButton[] 
                                                    {
                                                        InlineKeyboardButton.WithCallbackData("Прочие расходы", $"Прочие-{transactionId}"),
                                                    },
                                           });

                                var sendMessage = await botClient.SendTextMessageAsync(
                                       chat.Id,
                                       "Выберите категорию расхода",
                                       replyMarkup: inlineKeyboard);

                                UsersUpdateCategory[user.Id] = transactionId;
                            }
                            return;
                        }

                        if (callbackQuery.Data == "CancelUpdate")
                        {
                            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                            return;
                        }

                        if (callbackQuery.Data.StartsWith("ConfirmDelete"))
                        {
                            var transactionIdString = callbackQuery.Data.Replace("ConfirmDelete", "");
                            if (Guid.TryParse(transactionIdString, out var transactionId))
                            {
                                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);

                                string text = $"Запись {transaction.Type} {transaction.Amount} успешно удалена";

                                await transactionService.DeleteTransactionAsync(transactionId);

                                await botClient.EditMessageTextAsync(
                                     chatId: callbackQuery.Message.Chat.Id,
                                     messageId: (int)transaction.AmountMessageId,
                                     text: text);

                                await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                await SendMessageWithIncomeAndExpenseAsync(botClient, chat);
                            }

                            return;
                        }
                        
                        if (callbackQuery.Data == "CancelDelete")
                        {
                            await botClient.DeleteMessageAsync(
                                        chatId: callbackQuery.Message.Chat.Id,
                                        messageId: callbackQuery.Message.MessageId);
                            return;
                        }

                        return;
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
  
    private static decimal IsValidMoney(string? textMessage)
    {
        string sanitizedInput = textMessage.Trim();

        string pattern = @"^\d{1,3}(\s?\d{3})*(,|\.)?\d{0,2}$";

        if (Regex.IsMatch(sanitizedInput, pattern))
        {
            decimal check;
                 decimal.TryParse(
                sanitizedInput.Replace(" ", "").Replace(",", "."),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out check
            );

            return check;
        }

        return 0;
    }

    [Obsolete]
    private static async Task ChangeCurrecyAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, UserService userService, Telegram.Bot.Types.User user, Chat chat)
    {
        var userDb = await userService.GetUserAsync(user.Id);
        userDb.Currency = callbackQuery.Data;
        await userService.UpdateUserAsync(user.Id, userDb);
   
        await botClient.SendTextMessageAsync(
               chat.Id,
               "Вы выбрали " + callbackQuery.Data);
    }

    [Obsolete]
    private static async Task SendMessageWithIncomeAndExpenseAsync(ITelegramBotClient botClient, Chat chat)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(
               new List<InlineKeyboardButton[]>()
               {
            new InlineKeyboardButton[]
            {
                InlineKeyboardButton.WithCallbackData("Записать доход🔼", "Записать доход"),
            },
            new InlineKeyboardButton[]
            {
                 InlineKeyboardButton.WithCallbackData("Записать расход🔽", "Записать расход"),
            },

               });
        await botClient.SendTextMessageAsync(
                chat.Id,
                "Что добавим?",
                replyMarkup: inlineKeyboard);
    }

    [Obsolete]
    private static async Task<int> SendMessageWithTrackingAsync(ITelegramBotClient botClient, long chatId, string text, Guid transactionId)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(
                                                        new List<InlineKeyboardButton[]>()
                                                        {
                                                                    new InlineKeyboardButton[]
                                                                    {
                                                                        InlineKeyboardButton.WithCallbackData("Редактировать операцию🔄", $"Редактировать{transactionId}"),
                                                                    },
                                                                    new InlineKeyboardButton[]
                                                                    {
                                                                         InlineKeyboardButton.WithCallbackData("Удалить оперцию❌", $"Удалить{transactionId}"),
                                                                    },
                                                                      new InlineKeyboardButton[]
                                                                    {
                                                                        InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                                    },
                                                                        new InlineKeyboardButton[]
                                                                    {
                                                                        InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                                    },
                                                        });
        var sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            replyMarkup: inlineKeyboard);
        
        return sentMessage.Id;
    }

    [Obsolete]
    private static async Task EditMessageWithTrackingAsync(ITelegramBotClient botClient, long chatId, int messageId, string text, Guid transactionId)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(
                                                                   new List<InlineKeyboardButton[]>()
                                                                   {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Редактировать операцию🔄", $"Редактировать{transactionId}"),
                                                            },
                                                            new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Удалить оперцию❌", $"Удалить{transactionId}"),
                                                            },
                                                              new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Новая операция🆕", "Новая операция"),
                                                            },
                                                                new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Посмотреть статистику📊", "Посмотреть статистику"),
                                                            },
                                                                   });

        await botClient.EditMessageTextAsync(
            chatId: chatId,
            messageId: messageId,
            text: text,
            replyMarkup: inlineKeyboard);
    }

    [Obsolete]
    private static async Task SendMessageWithSuggestAddingDescription(ITelegramBotClient botClient, long chatId, Guid transactionId)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(
                                                                   new List<InlineKeyboardButton[]>()
                                                                   {
                                                            new InlineKeyboardButton[]
                                                            {
                                                                InlineKeyboardButton.WithCallbackData("Да", $"AddDescription{transactionId}"),
                                                            },
                                                            new InlineKeyboardButton[]
                                                            {
                                                                 InlineKeyboardButton.WithCallbackData("Нет", $"WithoutDescription{transactionId}"),
                                                            },
                                                                   });

        await botClient.SendTextMessageAsync(
           chatId: chatId,
           text: "Добавить описание?",
           replyMarkup: inlineKeyboard);
    }
}