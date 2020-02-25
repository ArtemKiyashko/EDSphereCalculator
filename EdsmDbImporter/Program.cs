﻿using AutoMapper;
using CommandLine;
using EdsmDbImporter.CalculatorModels;
using EdsmDbImporter.Extensions;
using EdsmDbImporter.Mappers;
using EdsmDbImporter.ResultWriters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EdsmDbImporter
{
    class Program
    {
        private static IDbImporter _dbImporter;
        private static IServiceProvider _serviceProvider;
        private static IServiceCollection _serviceCollection;
        private static IConfigurationRoot _configuration;
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            
            _configuration = builder.Build();
            
            _serviceCollection = new ServiceCollection();
            var parser = new Parser(with => with.CaseInsensitiveEnumValues = true);
            parser.ParseArguments<CmdOptions>(args)
                .WithParsed(RunApplication);
            Console.WriteLine("Press any key to stop");
            Console.ReadKey();
        }

        private static async void RunApplication(CmdOptions options)
        {
            _serviceProvider = _serviceCollection
                .AddSingleton(options)
                .AddAutoMapper(typeof(MapperProfile))
                .AddTransient<IResultWriter<string>, ConsoleDefaultWriter>()
                .AddTransient<IDataReader<EdSystem>>((_) => new DefaultDataReader<EdSystem>(options.EdsmStarDataPath, _.GetService<IResultWriter<string>>(), options.SkipSystems))
                .AddTransient<IDataReader<CelestialBody>>((_) => new DefaultDataReader<CelestialBody>(options.EdsmBodiesDataPath, _.GetService<IResultWriter<string>>(), options.SkipBodies))
                .AddDbContext<EdsmDbContext>(dbOptions =>
                {
                    dbOptions.UseLazyLoadingProxies();
                    dbOptions.UseNpgsql(_configuration.GetConnectionString("Postgre"), sqlOptions => sqlOptions.CommandTimeout(1800));
                })
                .AddTransient<DefaultDbImporter>()
                .AddTransient<IImportActionFactory, DefaultImportActionFactory>()
                .BuildServiceProvider();

            using var scope = _serviceProvider.CreateScope();
            _dbImporter = scope.ServiceProvider.GetService<DefaultDbImporter>();
            var st = new Stopwatch();
            st.Start();
            await _dbImporter.ImportSystemsAsync();
            st.Stop();
            var ts = st.Elapsed;
            Console.WriteLine("Import EdSystems. Elapsed Time is {0:00}:{1:00}:{2:00}.{3}",
                        ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

            var st1 = new Stopwatch();
            st1.Start();
            await _dbImporter.ImportBodiesAsync();
            st1.Stop();
            var ts1 = st1.Elapsed;
            Console.WriteLine("Import Celestial Bodies. Elapsed Time is {0:00}:{1:00}:{2:00}.{3}",
                        ts1.Hours, ts1.Minutes, ts1.Seconds, ts1.Milliseconds);

            Console.WriteLine("Done");
        }
    }
}