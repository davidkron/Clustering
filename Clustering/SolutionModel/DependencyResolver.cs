﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clustering.SolutionModel.Nodes;
using Microsoft.CodeAnalysis;

namespace Clustering.SolutionModel
{
    public class DependencyLink
    {
        public readonly Node Dependor;
        public readonly Node Dependency;

        public DependencyLink(Node dependor, Node dependency)
        {
            Dependor = dependor;
            Dependency = dependency;
        }
    }

    public static class DependencyResolver
    {
        public static IEnumerable<DependencyLink> GetDependencyList(IEnumerable<ClassNode> allClasses)
        {
            var allClassesBySymbol = allClasses.ToDictionary(x => x.Symbol, x => x);
            foreach (var dependor in allClassesBySymbol.Values)
            {
                foreach (var dependency in dependor.ClassProperties.SymbolDependencies)
                {
                    if (allClassesBySymbol.ContainsKey(dependency))
                        yield return new DependencyLink(dependor, allClassesBySymbol[dependency]);
                    else
                    {
                        var matchingSymbols = allClassesBySymbol.Keys.Where(x => SymbolsMatch(x, dependency)).ToList();
                        if (matchingSymbols.Count == 1)
                            yield return new DependencyLink(dependor, allClassesBySymbol[matchingSymbols.First()]);
                        if (matchingSymbols.Count > 1)
                            throw new NotImplementedException();
                    }
                }
            }
        }

        public static ILookup<Node, Node> GetDependencies(IEnumerable<ClassNode> allClasses)
            => GetDependencyList(allClasses)
                .ToLookup(x => x.Dependor,x => x.Dependency);
        

        private static bool SymbolsMatch(ISymbol symbol, ISymbol dependency)
        {
            return Equals(symbol, dependency) ||
                   symbol.MetadataName == dependency.MetadataName
                   && SymbolsMatch(symbol.ContainingSymbol, dependency.ContainingSymbol);
        }
    }
}
