﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Extentions.cs" company="">
//   
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace PEAssemblyReader
{
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Symbols;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// </summary>
    public static class Extentions
    {
        /// <summary>
        /// </summary>
        /// <param name="thisType">
        /// </param>
        /// <param name="type">
        /// </param>
        /// <returns>
        /// </returns>
        public static bool IsDerivedFrom(this IType thisType, IType type)
        {
            Debug.Assert(type != null, "type is null");

            if (thisType.Equals(type))
            {
                return false;
            }

            var t = thisType.BaseType;
            while (t != null)
            {
                if (type.TypeEquals(t))
                {
                    return true;
                }

                t = t.BaseType;
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <param name="other">
        /// </param>
        /// <returns>
        /// </returns>
        public static bool TypeEquals(this IType type, IType other)
        {
            return type != null && other.CompareTo(type) == 0;
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <param name="other">
        /// </param>
        /// <returns>
        /// </returns>
        public static bool TypeNotEquals(this IType type, IType other)
        {
            return !type.TypeEquals(other);
        }

        /// <summary>
        /// </summary>
        /// <param name="symbol">
        /// </param>
        /// <returns>
        /// </returns>
        internal static string CalculateNamespace(this Symbol symbol)
        {
            var namespaceSrc = symbol.ContainingNamespace;

            if (symbol.ContainingType.IsNestedType())
            {
                namespaceSrc = symbol.ContainingType.ContainingNamespace;
            }

            if (namespaceSrc == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            foreach (var namespacePart in namespaceSrc.ConstituentNamespaces)
            {
                if (namespacePart.IsGlobalNamespace)
                {
                    continue;
                }

                sb.Append(namespacePart);
            }

            return sb.ToString();
        }

        internal static IType ToType(this TypeSymbol typeSymbol)
        {
            return new MetadataTypeAdapter(typeSymbol);
        }

        internal static IDictionary<IType, IType> GenericMap(this IType type)
        {
            return GenericMap(type.GenericTypeParameters, type.GenericTypeArguments);
        }

        internal static IDictionary<IType, IType> GenericMap(this IMethod method)
        {
            return GenericMap(method.GetGenericParameters(), method.GetGenericArguments());
        }

        internal static IDictionary<IType, IType> GenericMap(IEnumerable<IType> parameters, IEnumerable<IType> arguments)
        {
            var typeParameters = parameters.ToList();
            var typeArguments = arguments.ToList();

            var map = new SortedDictionary<IType, IType>();
            for (var index = 0; index < typeArguments.Count; index++)
            {
                map[typeParameters[index]] = typeArguments[index];
            }

            return map;
        }

        internal static IType ResolveGeneric(this TypeSymbol typeSymbol, IGenericContext genericContext)
        {
            IType effectiveType = new MetadataTypeAdapter(typeSymbol);
            if (genericContext != null && !genericContext.IsEmpty)
            {
                if (typeSymbol.IsTypeParameter())
                {
                    if (genericContext.TypeSpecialization != null)
                    {
                        var newType = genericContext.TypeSpecialization.ResolveTypeParameter(effectiveType);
                        if (newType != null)
                        {
                            return newType;
                        }
                    }

                    if (genericContext.MethodSpecialization != null)
                    {
                        var newType = genericContext.MethodSpecialization.ResolveTypeParameter(effectiveType);
                        if (newType != null)
                        {
                            return newType;
                        }
                    }
                }

                var arrayType = typeSymbol as ArrayTypeSymbol;
                if (arrayType != null)
                {
                    return arrayType.ElementType.ResolveGeneric(genericContext).ToArrayType(arrayType.Rank);
                }

                var namedTypeSymbol = typeSymbol as NamedTypeSymbol;
                if (namedTypeSymbol != null)
                {
                    var metadataType = new MetadataTypeAdapter(namedTypeSymbol);
                    if (metadataType.IsGenericTypeDefinition)
                    {
                        var map = genericContext.TypeSpecialization.GenericMap();
                        var mapFilteredByTypeParameters = namedTypeSymbol.TypeArguments != null
                            ? SelectGenericsFromArguments(namedTypeSymbol, map)
                            : SelectGenericsFromParameters(namedTypeSymbol, map);

                        var newType = new ConstructedNamedTypeSymbol(
                            namedTypeSymbol.ConstructedFrom,
                            ImmutableArray.Create(mapFilteredByTypeParameters));

                        return new MetadataTypeAdapter(newType);
                    }
                }
            }

            return effectiveType;
        }

        private static TypeSymbol[] SelectGenericsFromParameters(NamedTypeSymbol namedTypeSymbol, IDictionary<IType, IType> map)
        {
            return map
                .Where(pair => (namedTypeSymbol.TypeParameters).Select(t => t).Any(tp => tp.Name == pair.Key.Name))
                .Select(pair => (pair.Value as MetadataTypeAdapter).TypeDef).ToArray();
        }

        private static TypeSymbol[] SelectGenericsFromArguments(NamedTypeSymbol namedTypeSymbol, IDictionary<IType, IType> map)
        {
            return map
                .Where(pair => (namedTypeSymbol.TypeArguments).Select(t => t).Any(tp => tp.Name == pair.Key.Name))
                .Select(pair => (pair.Value as MetadataTypeAdapter).TypeDef).ToArray();
        }

        internal static void AppendFullNamespace(this Symbol symbol, StringBuilder sb, string @namespace)
        {
            sb.Append(@namespace);
            if (sb.Length > 0)
            {
                sb.Append('.');
            }

            if (symbol.ContainingType != null && symbol.Kind != SymbolKind.TypeParameter)
            {
                sb.Append(symbol.ContainingType.ToType().Name);
                sb.Append('+');
            }
        }

        public static IMethodBody ResolveMethodBody(this IMethod methodInfo, IGenericContext genericContext)
        {
            return methodInfo.GetMethodBody()
                            ?? (genericContext != null && genericContext.MethodDefinition != null
                                ? genericContext.MethodDefinition.GetMethodBody(genericContext)
                                : null);
        }

        internal static MetadataDecoder GetMetadataDecoder(this PEModuleSymbol peModuleSymbol, IGenericContext genericContext)
        {
            if (genericContext != null)
            {
                if (genericContext.MethodDefinition != null)
                {
                    var contextMethod = ((MetadataMethodAdapter)genericContext.MethodDefinition).MethodDef as PEMethodSymbol;
                    return new MetadataDecoder(peModuleSymbol, contextMethod);
                }

                if (genericContext.TypeDefinition != null)
                {
                    var contextType = ((MetadataTypeAdapter)genericContext.TypeDefinition).TypeDef as PENamedTypeSymbol;
                    return new MetadataDecoder(peModuleSymbol, contextType);
                }
            }

            return new MetadataDecoder(peModuleSymbol);
        }
    }
}