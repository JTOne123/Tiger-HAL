﻿// <copyright file="TransformationMap.Builder{T}.cs" company="Cimpress, Inc.">
//   Copyright 2018 Cimpress, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using static Tiger.Hal.Properties.Resources;

namespace Tiger.Hal
{
    /// <summary>The builder for further transformations beyond "self".</summary>
    sealed partial class TransformationMap
    {
        /// <summary>Configures a created transformation map.</summary>
        /// <typeparam name="T">The type being transformed.</typeparam>
        internal class Builder<T>
            : ITransformationInstructions, ITransformationMap<T>
        {
            readonly Dictionary<string, ILinkInstruction> _links = new Dictionary<string, ILinkInstruction>();
            readonly List<IEmbedInstruction> _embeds = new List<IEmbedInstruction>();
            readonly List<IHoistInstruction> _hoists = new List<IHoistInstruction>();
            readonly List<string> _ignores = new List<string>();

            /// <summary>Initializes a new instance of the <see cref="Builder{T}"/> class.</summary>
            /// <param name="selfSelector">
            /// A function that creates an <see cref="ILinkData"/>
            /// from a value of type <typeparamref name="T"/>.
            /// </param>
            public Builder([NotNull] Func<T, ILinkData> selfSelector)
            {
                _links["self"] = new LinkInstruction<T>(selfSelector);
            }

            /// <inheritdoc/>
            IReadOnlyDictionary<string, ILinkInstruction> ITransformationInstructions.LinkInstructions => _links;

            /// <inheritdoc/>
            IReadOnlyCollection<IEmbedInstruction> ITransformationInstructions.EmbedInstructions => _embeds;

            /// <inheritdoc/>
            IReadOnlyCollection<IHoistInstruction> ITransformationInstructions.HoistInstructions => _hoists;

            /// <inheritdoc/>
            IReadOnlyCollection<string> ITransformationInstructions.IgnoreInstructions => _ignores;

            /// <summary>
            /// Gets the name of the selected property from <paramref name="selector"/>,
            /// if <paramref name="selector"/> represents a simple property selector.
            /// </summary>
            /// <typeparam name="TProperty">The return type of the selector.</typeparam>
            /// <param name="selector">The selector from wihch to get the name.</param>
            /// <returns>
            /// The name of the selected property, if <paramref name="selector"/> represents
            /// a simple property selector; otherwise, <see langword="null"/>.
            /// </returns>
            public static string GetSelectorName<TProperty>(Expression<Func<T, TProperty>> selector)
            {
                var parameter = selector.Parameters[0];

                return GetIgnoreNameCore(parameter.Name, selector.Body);

                string GetIgnoreNameCore(string name, Expression body)
                {
                    switch (body)
                    {
                        case BlockExpression be when be.Expressions.Count == 1:
                            return GetIgnoreNameCore(name, be.Expressions[0]);
                        case MemberExpression me when me.Expression is ParameterExpression pe && pe.Name == name:
                            return me.Member.Name;
                        case GotoExpression ge when ge.Kind == GotoExpressionKind.Return:
                            return GetIgnoreNameCore(name, ge.Value);
                        case UnaryExpression ue when ue.NodeType == ExpressionType.Convert:
                            return GetIgnoreNameCore(name, ue.Operand);
                        default:
                            return null;
                    }
                }
            }

            /* todo(cosborn)
             * Should expressions allow indexing, in the case of collections and dictionaries?
             */

            #region Link

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link(string relation, ILinkData linkData)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (linkData is null) { throw new ArgumentNullException(nameof(linkData)); }

                _links[relation] = new LinkInstruction<T>(_ => linkData);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link(
                string relation,
                Func<T, ILinkData> selector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (selector is null) { throw new ArgumentNullException(nameof(selector)); }

                _links[relation] = new LinkInstruction<T>(selector);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link(
                string relation,
                Func<T, Uri> selector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (selector is null) { throw new ArgumentNullException(nameof(selector)); }

                _links[relation] = new ConstantLinkInstruction<T>(selector);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link<TMember>(
                string relation,
                Func<T, IEnumerable<TMember>> collectionSelector,
                Func<TMember, ILinkData> linkSelector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (collectionSelector is null) { throw new ArgumentNullException(nameof(collectionSelector)); }
                if (linkSelector is null) { throw new ArgumentNullException(nameof(linkSelector)); }

                _links[relation] = new ManyLinkInstruction<T>(t => collectionSelector(t).Select(linkSelector));
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link<TMember>(
                string relation,
                Func<T, IEnumerable<TMember>> collectionSelector,
                Func<T, TMember, ILinkData> linkSelector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (collectionSelector is null) { throw new ArgumentNullException(nameof(collectionSelector)); }

                _links[relation] = new ManyLinkInstruction<T>(t => collectionSelector(t).Select(tm => linkSelector(t, tm)));
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link<TKey, TValue>(
                string relation,
                Func<T, IDictionary<TKey, TValue>> dictionarySelector,
                Func<TKey, TValue, ILinkData> linkSelector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (dictionarySelector is null) { throw new ArgumentNullException(nameof(dictionarySelector)); }

                _links[relation] = new ManyLinkInstruction<T>(
                    t => dictionarySelector(t).Select(kvp => linkSelector(kvp.Key, kvp.Value)));
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Link<TKey, TValue>(
                string relation,
                Func<T, IDictionary<TKey, TValue>> dictionarySelector,
                Func<T, TKey, TValue, ILinkData> linkSelector)
            {
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (dictionarySelector is null) { throw new ArgumentNullException(nameof(dictionarySelector)); }

                _links[relation] = new ManyLinkInstruction<T>(
                    t => dictionarySelector(t).Select(
                        kvp => linkSelector(t, kvp.Key, kvp.Value)));
                return this;
            }

            #endregion

            #region Embed

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Embed<TMember>(
                string relation,
                Expression<Func<T, TMember>> memberSelector,
                Func<T, ILinkData> linkSelector)
            {
                if (memberSelector is null) { throw new ArgumentNullException(nameof(memberSelector)); }
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (linkSelector is null) { throw new ArgumentNullException(nameof(linkSelector)); }

                switch (memberSelector.Body)
                {
                    case MemberExpression me:
                        var valueSelector = memberSelector.Compile();
                        _links[relation] = new LinkInstruction<T>(linkSelector);
                        _embeds.Add(new MemberEmbedInstruction<T, TMember>(relation, me.Member.Name, valueSelector));
                        return this;
                    default:
                        throw new ArgumentException(MalformedValueSelector);
                }
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Embed<TMember>(
                string relation,
                Expression<Func<T, TMember>> memberSelector,
                Func<T, TMember, ILinkData> linkSelector)
            {
                if (memberSelector is null) { throw new ArgumentNullException(nameof(memberSelector)); }
                if (relation is null) { throw new ArgumentNullException(nameof(relation)); }
                if (linkSelector is null) { throw new ArgumentNullException(nameof(linkSelector)); }

                switch (memberSelector.Body)
                { // todo(cosborn) Allow indexing, in the case of collections and dictionaries?
                    case MemberExpression me:
                        var valueSelector = memberSelector.Compile();
                        _links[relation] = new LinkInstruction<T>(t => linkSelector(t, valueSelector(t)));
                        _embeds.Add(new MemberEmbedInstruction<T, TMember>(relation, me.Member.Name, valueSelector));
                        return this;
                    default:
                        throw new ArgumentException(MalformedValueSelector);
                }
            }

            #endregion

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Hoist<TMember>(Expression<Func<T, TMember>> memberSelector)
            {
                if (memberSelector is null) { throw new ArgumentNullException(nameof(memberSelector)); }

                var name = GetSelectorName(memberSelector);
                if (name is null)
                {
                    throw new ArgumentException(MalformedValueSelector, nameof(memberSelector));
                }

                var valueSelector = memberSelector.Compile();
                _hoists.Add(new MemberHoistInstruction<T, TMember>(name, valueSelector));
                return this;
            }

            #region Ignore

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Ignore(string memberSelector1)
            {
                if (memberSelector1 is null) { throw new ArgumentNullException(nameof(memberSelector1)); }

                _ignores.Add(memberSelector1);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Ignore(string memberSelector1, string memberSelector2)
            {
                if (memberSelector1 is null) { throw new ArgumentNullException(nameof(memberSelector1)); }
                if (memberSelector2 is null) { throw new ArgumentNullException(nameof(memberSelector2)); }

                _ignores.Add(memberSelector1);
                _ignores.Add(memberSelector2);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Ignore(
                string memberSelector1,
                string memberSelector2,
                string memberSelector3)
            {
                if (memberSelector1 is null) { throw new ArgumentNullException(nameof(memberSelector1)); }
                if (memberSelector2 is null) { throw new ArgumentNullException(nameof(memberSelector2)); }
                if (memberSelector3 is null) { throw new ArgumentNullException(nameof(memberSelector3)); }

                _ignores.Add(memberSelector1);
                _ignores.Add(memberSelector2);
                _ignores.Add(memberSelector3);
                return this;
            }

            /// <inheritdoc/>
            ITransformationMap<T> ITransformationMap<T>.Ignore(params string[] memberSelectors)
            {
                if (memberSelectors is null) { throw new ArgumentNullException(nameof(memberSelectors)); }

                _ignores.AddRange(memberSelectors);
                return this;
            }

            #endregion
        }
    }
}