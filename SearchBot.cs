﻿using HotCookies;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using PuppeteerSharp;
using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public class SearchBot
{
    private static readonly Random random = new Random();

    private ConfigurationModel? configuration;
    Browser browser = null;
    private static readonly SemaphoreSlim serverSemaphore = new SemaphoreSlim(1, 1);

    private static readonly ILogger logger = Log.ForContext<SearchBot>();


    // Настройка логгера

    public async Task Run()
    {
        SetupLogger();

        try
        {
            LoadConfiguration();

            List<Profile> profiles = await ProfileManager.GetProfiles();

            // Поиск профилей по группе
            List<Profile> selectedProfiles = profiles.Where(p => p.GroupName == configuration?.ProfileGroupName).ToList();
            if (selectedProfiles.Count == 0)
            {
                return;
            }

            List<Task> tasks = new List<Task>();

            foreach (Profile profile in selectedProfiles)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (profile is null) return;

                        await serverSemaphore.WaitAsync(); // Ожидаем доступ к серверу
                        var browser = await BrowserManager.ConnectBrowser(profile.UserId);
                        serverSemaphore.Release(); // Освобождаем доступ к серверу

                        if (browser == null)
                        {
                            return;
                        }
                                            


                        var page = await browser.NewPageAsync();
                        await page.GoToAsync("https://www.google.com");

                        Random random = new Random();
                        int randomVisitCount = random.Next(configuration.MinSiteVisitCount, configuration.MaxSiteVisitCount);

                        for (int i = 0; i < randomVisitCount; i++)
                        {
                            await PerformSearch(page, GetRandomSearchQuery());
                            await SpendRandomTime();
                            await ClickRandomLink(page);

                            await serverSemaphore.WaitAsync(); // Ожидаем доступ к серверу перед закрытием страницы
                            //await page.CloseAsync();
                            serverSemaphore.Release(); // Освобождаем доступ к серверу
                        }

                        await serverSemaphore.WaitAsync(); // Ожидаем доступ к серверу перед закрытием браузера
                        var pages = await browser.PagesAsync();
                        foreach (var p in pages)
                        {
                            await p.CloseAsync();
                            await page.WaitForTimeoutAsync(500);
                        }
                        await page.WaitForTimeoutAsync(500);
                        await browser.CloseAsync();

                        serverSemaphore.Release(); // Освобождаем доступ к серверу
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Произошла ошибка в методе Run {ex}");
                        // Обработка ошибок                        
                    }
                }));
            }

            await Task.WhenAll(tasks);

            
        }
        catch (Exception ex)
        {
            // Обработка ошибок
            logger.Error($"Произошла ошибка в методе Run {ex}");
        }
    }
    
    private async Task PerformSearch(IPage page, string searchQuery)
    {
        try
        {
            await page.WaitForSelectorAsync("input[name='q']");
            await page.FocusAsync("input[name='q']");
            await page.Keyboard.PressAsync("End");

            var inputValue = await page.EvaluateExpressionAsync<string>("document.querySelector('input[name=\"q\"]').value");
            for (int i = 0; i < inputValue.Length; i++)
            {
                try
                {
                    await page.Keyboard.PressAsync("Backspace");

                    Random randomDelay = new Random();
                    int typeDelay = randomDelay.Next(200, 700);
                    await page.WaitForTimeoutAsync(typeDelay);
                }
                catch (Exception ex)
                {                    
                    logger.Error($"Ошибка в методе PerformSearch {ex}");                    
                }
            }

            try
            {
                await page.TypeAsync("input[name='q']", searchQuery);
                await page.Keyboard.PressAsync("Enter");
            }
            catch (Exception ex)
            {                
                logger.Error($"Ошибка в методе PerformSearch {ex}");
            }
        }
        catch (Exception ex)
        {
            // Обработка ошибок, возникающих при выполнении операций внутри метода PerformSearch
            logger.Error($"Ошибка в методе PerformSearch {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private async Task ClickRandomLink(IPage page)
    {
        try
        {
            var clickedLinks = new List<string>();
            int maxSiteVisitCount = configuration.MaxSiteVisitCount;

            while (clickedLinks.Count < maxSiteVisitCount)
            {
                try
                {
                    var linkElements = await page.QuerySelectorAllAsync(".A9xod.ynAwRc.ClLRCd.q8U8x.MBeuO.oewGkc.LeUQr");

                    foreach (var linkElement in linkElements)
                    {
                        try
                        {
                            var linkText = await linkElement.EvaluateFunctionAsync<string>("el => el.innerText");

                            if (!clickedLinks.Contains(linkText))
                            {
                                await page.EvaluateFunctionAsync(@"(element) => {
                                const y = element.getBoundingClientRect().top + window.pageYOffset;
                                const duration = 1000; // Длительность анимации в миллисекундах
                                const increment = 20; // Шаг прокрутки за один кадр

                                const scrollToY = (to, duration) => {
                                    if (duration <= 0) return;
                                    const difference = to - window.pageYOffset;
                                    const perTick = difference / duration * increment;

                                    setTimeout(() => {
                                        window.scrollBy(0, perTick);
                                        if (window.pageYOffset === to) return;
                                        scrollToY(to, duration - increment);
                                    }, increment);
                                }

                                scrollToY(y, duration);
                            }", linkElement);

                                await page.WaitForTimeoutAsync(2000);

                                await linkElement.ClickAsync();

                                clickedLinks.Add(linkText);

                                await SimulateUserBehavior(page);

                                await page.GoBackAsync();

                                await page.WaitForTimeoutAsync(2000);

                                break; // Прерываем цикл после успешного клика
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Ошибка в методе ClickRandomLink {ex}");
                            await page.ReloadAsync();
                            await page.WaitForTimeoutAsync(10000);

                            linkElements = await page.QuerySelectorAllAsync(".A9xod.ynAwRc.ClLRCd.q8U8x.MBeuO.oewGkc.LeUQr");
                             continue;                            
                        }
                    }

                    // Если все ссылки уже были посещены, прокручиваем страницу
                    if (clickedLinks.Count == linkElements.Length)
                    {
                        await page.EvaluateFunctionAsync(@"() => {
                        const scrollHeight = Math.max(document.documentElement.scrollHeight, document.body.scrollHeight);
                        const clientHeight = document.documentElement.clientHeight;
                        const duration = 3000; // Длительность анимации в миллисекундах
                        const increment = 20; // Шаг прокрутки за один кадр

                        const scrollToBottom = (duration) => {
                            if (duration <= 0) return;
                            const difference = scrollHeight - window.pageYOffset - clientHeight;
                            const perTick = difference / duration * increment;

                            setTimeout(() => {
                                window.scrollBy(0, perTick);
                                if (window.pageYOffset + clientHeight === scrollHeight) return;
                                scrollToBottom(duration - increment);
                            }, increment);
                        }

                        scrollToBottom(duration);
                    }");

                        // Ждем, пока страница прокрутится и новые элементы загрузятся
                        await page.WaitForTimeoutAsync(3000);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Ошибка в методе ClickRandomLink {ex}");
                    await page.ReloadAsync();
                    await page.WaitForTimeoutAsync(10000);
                    continue;
                    // Логирование ошибки или предпринятие других действий по обработке ошибки
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Ошибка в методе ClickRandomLink {ex}");
            return;
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private async Task SimulateUserBehavior(IPage page)
    {
        try
        {                     
            await page.WaitForTimeoutAsync(10000);

            int minTimeSpent = configuration.MinTimeSpent;
            int maxTimeSpent = configuration.MaxTimeSpent;

            var randomTime = new Random().Next(minTimeSpent, maxTimeSpent + 1) * 1000; // Преобразуем время в миллисекунды
            var endTime = DateTime.UtcNow.AddMilliseconds(randomTime);

            while (DateTime.UtcNow < endTime)
            {
                try
                {
                    var scrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
                    var windowHeight = await page.EvaluateExpressionAsync<int>("window.innerHeight");
                    var currentScroll = await page.EvaluateExpressionAsync<int>("window.scrollY");

                    if (currentScroll + windowHeight >= scrollHeight)
                    {
                        // Достигнут нижний конец страницы, прокручиваем вверх
                        await ScrollPageSmoothly(page, ScrollDirection.Up);
                        await page.WaitForTimeoutAsync(1000); // Добавляем небольшую задержку между прокрутками
                    }
                    else
                    {
                        // Продолжаем прокручивать вниз
                        await ScrollPageSmoothly(page, ScrollDirection.Down);
                        await page.WaitForTimeoutAsync(1500); // Добавляем небольшую задержку между прокрутками
                    }
                }
                catch (Exception ex)
                {
                    // Обработка ошибок, возникающих при симуляции поведения пользователя
                    logger.Error($"Ошибка в методе SimulateUserBehavior {ex}");
                    continue;
                    // Логирование ошибки или предпринятие других действий по обработке ошибки
                }
            }
        }
        catch (Exception ex)
        {
            // Обработка ошибок, возникающих при выполнении операций внутри метода SimulateUserBehavior
            logger.Error($"Ошибка в методе SimulateUserBehavior {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private async Task ScrollPageSmoothly(IPage page, ScrollDirection direction)
    {
        try
        {
            if (page.IsClosed)
            {
                return; // Прекращаем выполнение, если страница закрыта
            }

            var scrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
            var windowHeight = await page.EvaluateExpressionAsync<int>("window.innerHeight");
            var currentScroll = await page.EvaluateExpressionAsync<int>("window.scrollY");

            var scrollStep = 150;

            // Задержка между прокруткой

            if (direction == ScrollDirection.Down)
            {
                while (currentScroll + windowHeight < scrollHeight)
                {
                    try
                    {
                        currentScroll += scrollStep;
                        await page.EvaluateFunctionAsync(@"(scrollStep) => {
                        window.scrollBy(0, scrollStep);
                    }", scrollStep);

                        Random randomDelay = new Random();
                        int scrollDelay = randomDelay.Next(200, 1000);
                        await page.WaitForTimeoutAsync(scrollDelay);
                    }
                    catch (Exception ex)
                    {
                        // Обработка ошибок, возникающих при прокрутке страницы вниз
                        logger.Error($"Ошибка в методе ScrollPageSmoothly {ex}");
                        continue;
                        // Логирование ошибки или предпринятие других действий по обработке ошибки
                    }
                }
            }
            else if (direction == ScrollDirection.Up)
            {
                while (currentScroll > 0)
                {
                    try
                    {
                        currentScroll -= scrollStep;
                        await page.EvaluateFunctionAsync(@"(scrollStep) => {
                        window.scrollBy(0, -scrollStep);
                    }", scrollStep);

                        Random randomDelay = new Random();
                        int scrollDelay = randomDelay.Next(200, 1000);
                        await page.WaitForTimeoutAsync(scrollDelay);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Ошибка в методе ScrollPageSmoothly {ex}");
                        // Обработка ошибок, возникающих при прокрутке страницы вверх
                        continue;
                        // Логирование ошибки или предпринятие других действий по обработке ошибки
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Обработка ошибок, возникающих при выполнении операций внутри метода ScrollPageSmoothly
            logger.Error($"Ошибка в методе ScrollPageSmoothly {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private enum ScrollDirection
    {
        Up,
        Down
    }

    private void LoadConfiguration()
    {
        try
        {
            string json = File.ReadAllText("config.json");
            configuration = JsonConvert.DeserializeObject<ConfigurationModel>(json);
        }
        catch (Exception ex)
        {
            // Обработка ошибки при загрузке и десериализации конфигурации
            logger.Error($"Ошибка в методе LoadConfiguration {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private string GetRandomSearchQuery()
    {
        try
        {
            string[] queries = configuration.SearchQueries?.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (queries.Length == 0)
            {                
                return string.Empty;
            }

            int randomIndex = random.Next(queries.Length);
            return queries[randomIndex];
        }
        catch (Exception ex)
        {
            // Обработка ошибки при получении случайного поискового запроса
            logger.Error($"Ошибка в методе GetRandomSearchQuery {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
            return string.Empty; // Возвращаем пустую строку или другое значение по умолчанию в случае ошибки
        }
    }

    private async Task SpendRandomTime()
    {
        try
        {
            int time = random.Next(configuration.MinTimeSpent, configuration.MaxTimeSpent + 1);
            await Task.Delay(time * 1000);
        }
        catch (Exception ex)
        {
            logger.Error($"Ошибка в методе SpendRandomTime {ex}");
            // Логирование ошибки или предпринятие других действий по обработке ошибки
        }
    }

    private void SetupLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs.txt") // Укажите путь к файлу логов
            .CreateLogger();
    }
}
