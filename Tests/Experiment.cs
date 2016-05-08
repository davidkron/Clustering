﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clustering;
using Clustering.Benchmarking;
using Clustering.Hierarchichal;
using Clustering.Hierarchichal.CuttingAlgorithms;
using Clustering.SimilarityMetrics.MojoFM;
using Clustering.SolutionModel.Serializing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.Building.TestExtensions;

namespace Tests
{
    [TestClass]
    public class Benchmark
    {
        private readonly Dictionary<string, Repository> _repositories = 
            new List<Repository>
            {
                new Repository("MonoGame", "Mono", "MonoGame.Framework.Windows.sln"),
                new Repository("octokit.net", "octokit", "Octokit.sln"), // ---- CURRENTLY MISSING FROM AVAILIBLE PARSED DATA
                new Repository("DotNetOpenAuth", "DotNetOpenAuth", "src\\DotNetOpenAuth.sln"),
                new Repository("fluentmigrator", "schambers", "---- !!! FILL THIS IN ANDREAS  !!! ----"),
                new Repository("shadowsocks-windows", "shadowsocks", "---- !!! FILL THIS IN ANDREAS !!! ----"),
                new Repository("SignalR", "SignalR", "Microsoft.AspNet.SignalR.sln")
            }.ToDictionary(x => x.Name, x => x);

        private string currentRepoToTest = "Fail";

        // Data we now for sure is correct and has been parsed after all parse related bugs where resolved
        private readonly IEnumerable<string> _availibleParsedData = new List<string>()
        {
            "MonoGame",
            "DotNetOpenAuth",
            "SignalR",
            "fluentmigrator",
            "shadowsocks-windows"
        };

        // TESTS
        [TestMethod]
        public void PrepareData()
        {
            SolutionBenchmark.Prepare(_repositories[currentRepoToTest]);
        }

        [TestMethod]
        public void RunSpecificBenchmark()
        {
            var markConfig = new WeightedCombinedStaticMojoFm();
            SolutionBenchmark.RunAllInFolder(new List<IBenchmarkConfig> {markConfig},
                new List<Repository> {_repositories[currentRepoToTest]});
        }

        [TestMethod]
        public void BenchAllAvailibleData()
        {
            SolutionBenchmark.RunAllInFolder(
                new List<IBenchmarkConfig>
                {
                    new WeightedCombinedStaticMojoFm(),
                    new WeightedCombinedSymmetricHalfMojoFm(),
                    new WeightedCombinedSepUsage()
                },
                _availibleParsedData.Select(x => _repositories[x]).ToList())
                .WriteToFolder(Paths.SolutionFolder + "BenchMarkResults\\");
        }
    }
}
