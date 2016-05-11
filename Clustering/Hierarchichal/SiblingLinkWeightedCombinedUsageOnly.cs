﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Clustering.SolutionModel;
using Clustering.SolutionModel.Nodes;
using MoreLinq;

namespace Clustering.Hierarchichal
{

    public class SiblingLinkWeightedCombinedUsageOnly : ClusteringAlgorithm
    {
        private Dictionary<Node, FeatureVector> _featureVectors;

        protected override void Setup(ISet<Node> nodes, ILookup<Node, Node> edges)
        {
            var dependencies = nodes.ToDictionary(x => x, x => new HashSet<Node>(edges[x]));

            var usages
                = nodes.ToDictionary(
                dependency => dependency,
                dependency => new HashSet<Node>());

            foreach (var edge in dependencies)
            {
                var dependent = edge.Key;
                foreach (var dependency in edge.Value)
                {
                    // TODO: Is this check really needed ?
                    if (!usages.ContainsKey(dependency))
                        usages[dependency] = new HashSet<Node>();
                    usages[dependency].Add(dependent);
                }
            }

            _featureVectors = nodes.ToDictionary(
                node => node,
                node => new FeatureVector(usages[node].ToSet()));
        }

        protected override SimilarityMatrix CreateSimilarityMatrix(ISet<Node> nodes)
        {
            var simMatrix = new SimilarityMatrix();

            var pairs = from left in nodes
                from right in nodes
                where left.GetHashCode() < right.GetHashCode()
                select new {left, right};

            foreach (var pair in pairs)
                simMatrix.Add(pair.left, pair.right, Similarity(_featureVectors[pair.left], _featureVectors[pair.right]));

            return simMatrix;
        }


        protected override void UpdateSimilarityMatrix(Node item1, Node item2, ClusterNode clusterNode,
            SimilarityMatrix matrix)
        {
            _featureVectors.Add(clusterNode, _featureVectors[item1]
                .Merge(_featureVectors[item2]));

            foreach (var node in _nodes.Where(node => node != item1 && node != item2))
            {
                matrix.Add(node, clusterNode, Similarity(_featureVectors[node],
                    _featureVectors[clusterNode]));
                matrix.Remove(node, item1);
                matrix.Remove(node, item2);
            }

            matrix.Remove(item1, item2);

            _featureVectors.Remove(item1);
            _featureVectors.Remove(item2);
        }

        public override double Similarity(FeatureVector a, FeatureVector b)
        {
            var both = a.Intersect(b);
            var onlyA = a.Except(b);
            var onlyB = b.Except(a);

            var MaHalf = both.Sum(x => a[x]/a.Total
                                       + b[x]/b.Total)*0.5;

            var Mb = onlyA.Sum(x => a[x])/a.Total;
            var Mc = onlyB.Sum(x => b[x])/b.Total;

            if (MaHalf + Mb + Mc <= 0) return 0;
            // MaHalf + Mb + Mc > 0
            return MaHalf / (MaHalf + Mb + Mc);
        }
    }
}