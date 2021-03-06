using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Clustering.SolutionModel;
using MoreLinq;

namespace Clustering.Benchmarking.Results
{

    public class FormattedTable
    {
        public readonly List<string> Lines;

        public FormattedTable(List<List<string>> lines)
        {
            Lines = ToFormattedLines(lines).ToList();
        }

        private IEnumerable<string> ToFormattedLines(List<List<string>> lines)
        {
            var columns = lines.First().Count;
            //Same number of columns on each line
            Debug.Assert(lines.All(x => x.Count == columns));
            
            var columnWidths = Enumerable.Range(0, columns)
                .Select(index => lines.Select(x => x[index]).Max(x => x.Length) + 1).ToList();

            foreach (var line in lines)
            {
                var formattedLine = "";
                for (var i = 0; i < columns; i++)
                {
                    var content = line[i];
                    formattedLine += content;
                    if(i == columns -1) // dont add space after last column
                        break;
                    var spaces = columnWidths[i] - content.Length;
                    formattedLine += new string(' ', spaces);
                }
                yield return formattedLine;
            }

        } 
    }

    public class ResultsTable
    {
        public readonly ISet<AlgorithmName> Algorithms;
        public readonly ISet<RepoName> Repositories;
        public readonly IDictionary<AlgorithmName, int> TotalRuns;
        public readonly IDictionary<Tuple<RepoName, AlgorithmName>, double> Scores; 

        public ResultsTable(IReadOnlyCollection<Score> scoreList,
            IDictionary<AlgorithmName,int> runPerAlgrotithm )
        {
            Algorithms = scoreList.Select(score => score.Algorithm).ToHashSet();
            Repositories = scoreList.Select(score => score.Repository).ToHashSet();
            Scores = scoreList.ToDictionary(x => Tuple.Create(x.Repository, x.Algorithm), score => score.Value);
            TotalRuns = runPerAlgrotithm;
        }

        public double ResultFor(RepoName repo, AlgorithmName alg) => Scores[Tuple.Create(repo, alg)];

        public ResultsTable Combine(ResultsTable other)
        {
            if (Repositories == other.Repositories)
                return null;
            var algorithmsInBoth = Algorithms.Intersect(other.Algorithms).ToSet();
            var onlyInA = Algorithms.Except(other.Algorithms).ToSet();
            var onlyInB = other.Algorithms.Except(Algorithms).ToSet();
            var newScores = new List<Score>();

            var totalRunsInBoth = algorithmsInBoth
                .ToDictionary(alg => alg, alg => TotalRuns[alg] + other.TotalRuns[alg]);

            var totalRunsNew = totalRunsInBoth
                .Union(onlyInA.ToDictionary(x => x, x => TotalRuns[x]))
                .Union(onlyInB.ToDictionary(x => x, x => other.TotalRuns[x]))
                .ToDictionary(x => x.Key, x => x.Value);

            foreach (var repository in Repositories)
            {
                newScores.AddRange(
                    from algorithm in algorithmsInBoth
                    let key = Tuple.Create(repository, algorithm)
                    let totalA = Scores[key] * TotalRuns[algorithm]
                    let totalB = other.Scores[key] * other.TotalRuns[algorithm]
                    let newAverage = (totalA + totalB) / totalRunsInBoth[algorithm]
                    select new Score(repository, algorithm, newAverage));

                newScores.AddRange(onlyInA.Select(algorithmName
                    => new Score(repository, algorithmName, Scores[Tuple.Create(repository, algorithmName)])));

                newScores.AddRange(onlyInB.Select(algorithmName
                    => new Score(repository, algorithmName, other.Scores[Tuple.Create(repository, algorithmName)])));
            }

            return new ResultsTable(newScores, totalRunsNew);
        }

        public static ResultsTable Parse(string file) => Parse(File.ReadAllLines(file));

        public static ResultsTable Parse(IEnumerable<string> lines)
        {
            var table = lines.Select(x => x.Split(new char[0],
                StringSplitOptions.RemoveEmptyEntries).ToList()).ToList();

            var algorithms = table.First().Skip(1).Select(x => new AlgorithmName { Name = x }).ToList();
            var withoutHeader = table.Skip(1).ToList();
            var withoutTotalRuns = withoutHeader.Take(withoutHeader.Count - 1);

            var scores = new List<Score>();
            foreach (var line in withoutTotalRuns)
            {
                var repo = new RepoName { Name = line.First() };
                var scorePerAlgorithm = line.Skip(1).ToList();
                Debug.Assert(scorePerAlgorithm.Count == algorithms.Count);
                scores.AddRange(algorithms.Select((algorithm, i)
                    => new Score(repo, algorithm, Convert.ToDouble(scorePerAlgorithm[i], CultureInfo.InvariantCulture))));
            }

            var runsRow = withoutHeader.Last().Skip(1).ToList();

            var runsPerAlg = runsRow.Select((x, i) => Tuple.Create(algorithms[i], Convert.ToInt32(x)))
                .ToDictionary(x => x.Item1, x => x.Item2);

            return new ResultsTable(scores, runsPerAlg);
        }

        public void MergeAndWriteWith(string path)
        {
            var tableToWrite = this;
            if (File.Exists(path))
            {
                var existing = Parse(path);
                tableToWrite = Combine(existing);
            }
            tableToWrite.WriteTo(path);
        }

        public List<string> FormattedLines()
        {
            var matrix = new List<List<string>>();
            var algorithmList = Algorithms.OrderBy(x => x.Name).ToList();

            var firstLine = new List<string> { "Repo/Algorithm" };
            firstLine.AddRange(algorithmList.Select(x => x.Name));

            var scoreLines = from repo in Repositories
                             select new List<string> { repo.Name }
                                .Concat(algorithmList.Select(x => ResultFor(repo, x)
                                   .ToString(CultureInfo.InvariantCulture))).ToList();
            
            var totalRunsLine = new List<string> { "TOTAL-RUNS" };
            totalRunsLine.AddRange(algorithmList.Select(x => TotalRuns[x].ToString()));

            matrix.Add(firstLine);
            matrix.AddRange(scoreLines);
            matrix.Add(totalRunsLine);
            return new FormattedTable(matrix).Lines;
        }

        private void WriteTo(string path) => File.WriteAllLines(path, FormattedLines());

        public void WriteLatex(string path)
        {
            var lines = new List<string>();
            //var algorithms = new List<string> { "WCAS-Halfcut", "WCASU-Halfcut", "WCAD-Halfcut", "WCAUO-Halfcut" };
            var algorithms = new List<string> { "WCAS-Unbiased", "WCASep-Unbiased", "WCADepOnly-Unbiased", "WCAUsageOnly-Unbiased" };
            var algorithmList = algorithms.Select(t => Algorithms.First(x => x.Name == t)).ToList();
            var results = algorithmList.Select(algorithm => new List<double>()).ToList();

            lines.Add(@"\textbf{System} & \textbf{Original WCA} & \textbf{Separate WCA} & \textbf{Dependencies Only} & \textbf{Usage Only} \\ \hline");

            foreach (var repo in Repositories.OrderBy(x => x.Name))
            {
                for(int i = 0; i < algorithmList.Count ; i++)
                    results[i].Add(ResultFor(repo, algorithmList[i]));
                lines.Add(string.Format(@"\resitem{{{0}}}{{{1}}}{{{2}}}{{{3}}}{{{4}}}",
                    repo.Name,
                    results[0].Last().ToString("F1", CultureInfo.InvariantCulture),
                    results[1].Last().ToString("F1", CultureInfo.InvariantCulture),
                    results[2].Last().ToString("F1", CultureInfo.InvariantCulture),
                    results[3].Last().ToString("F1", CultureInfo.InvariantCulture)));
            }
            
            lines.Add(string.Format(CultureInfo.InvariantCulture, @"\textbf{{Average}} & \textbf{{{0:F1}\%}} & \textbf{{{1:F1}\%}} & \textbf{{{2:F1}\%}} & \textbf{{{3:F1}\%}} \\ \hline",
                results[0].Average(),
                results[1].Average(),
                results[2].Average(),
                results[3].Average()));
            
            File.WriteAllLines(path, lines);
        }

        public void WriteLatexDependencies(string path)
        {
            var lines = new List<string>();
            var algorithms = new List<string> { "WCADepOnly-Unbiased", "WCADepOnly-Unbiased-50%", "WCADepOnly-Unbiased-25%" };
            var algorithmList = algorithms.Select(t => Algorithms.First(x => x.Name == t)).ToList();
            var results = algorithmList.Select(algorithm => new List<double>()).ToList();

            lines.Add(@"\textbf{System} & \textbf{100\%} & \textbf{50\%} & \textbf{25\%} \\");
            lines.Add(@"\midrule");

            foreach (var repo in Repositories.OrderBy(x => x.Name))
            {
                for (int i = 0; i < algorithmList.Count; i++)
                    results[i].Add(ResultFor(repo, algorithmList[i]));
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    @"{0} & {1:F1}\% & {2:F1}\% & {3:F1}\% \\",
                    repo.Name,
                    results[0].Last(),
                    results[1].Last(),
                    results[2].Last()));
            }

            lines.Add(@"\midrule");
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                @"\textbf{{Average}} & \textbf{{{0:F1}\%}} & \textbf{{{1:F1}\%}} & \textbf{{{2:F1}\%}} \\",
                results[0].Average(),
                results[1].Average(),
                results[2].Average()));

            File.WriteAllLines(path, lines);
        }

        public void WriteLatexDepCompare(string path)
        {
            var lines = new List<string>();
            var algorithms = new List<string> { "Dep->Usage-Equallity" };
            var algorithmList = algorithms.Select(t => Algorithms.First(x => x.Name == t)).ToList();
            var results = algorithmList.Select(algorithm => new List<double>()).ToList();

            lines.Add(@"\textbf{System} & \textbf{100\%} \\");
            lines.Add(@"\midrule");

            foreach (var repo in Repositories.OrderBy(x => x.Name))
            {
                for (int i = 0; i < algorithmList.Count; i++)
                    results[i].Add(ResultFor(repo, algorithmList[i]));
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    @"{0} & {1:F1}\% \\",
                    repo.Name,
                    results[0].Last()));
            }

            lines.Add(@"\midrule");
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                @"\textbf{{Average}} & \textbf{{{0:F1}\%}}\\",
                results[0].Average()));

            File.WriteAllLines(path, lines);
        }
    }

    public struct AlgorithmName
    {
        public string Name;
        public AlgorithmName(string name)
        {
            Name = name;
        }
    }

    public struct RepoName
    {
        public string Name;
        public RepoName(string name)
        {
            Name = name;
        }
    }

    public struct Score
    {
        public readonly AlgorithmName Algorithm;
        public readonly RepoName Repository;
        public readonly double Value;

        public Score(RepoName repository, AlgorithmName algorithm, double value)
        {
            Repository = repository;
            Algorithm = algorithm;
            Value = value;
        }
    }
}