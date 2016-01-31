﻿namespace Il2Native.Logic
{
    using System;
    using System.Text;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Symbols;

    public static class Helpers
    {
        public static string CleanUpNameAllUnderscore(this string name)
        {
            if (name == null)
            {
                return null;
            }

            var s = new char[name.Length];

            var n = ' ';
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                switch (c)
                {
                    case '.':
                    case ':':
                    case '<':
                    case '>':
                    case '-':
                    case ',':
                    case '*':
                    case '[':
                    case ']':
                    case '&':
                    case '(':
                    case ')':
                    case '{':
                    case '}':
                    case '$':
                    case '=':
                        n = '_';
                        break;
                    default:
                        n = c;
                        break;
                }

                s[i] = n;
            }

            return new string(s);
        }

        public static string CleanUpName(this string name)
        {
            if (name == null)
            {
                return null;
            }

            var s = new char[name.Length];

            var n = ' ';
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                switch (c)
                {
                    case ' ':
                        n = '_';
                        break;
                    case '.':
                        n = '_';
                        break;
                    case ':':
                        n = '_';
                        break;
                    case '<':
                        n = 'G';
                        break;
                    case '>':
                        n = 'C';
                        break;
                    case '-':
                        n = '_';
                        break;
                    case ',':
                        n = '_';
                        break;
                    case '*':
                        n = 'P';
                        break;
                    case '[':
                        n = 'A';
                        break;
                    case ']':
                        n = 'Y';
                        break;
                    case '&':
                        n = 'R';
                        break;
                    case '(':
                        n = 'F';
                        break;
                    case ')':
                        n = 'N';
                        break;
                    case '{':
                        n = 'C';
                        break;
                    case '}':
                        n = 'Y';
                        break;
                    case '$':
                        n = 'D';
                        break;
                    case '=':
                        n = 'E';
                        break;
                    case '`':
                        n = 'T';
                        break;
                    default:
                        n = c;
                        break;
                }

                s[i] = n;
            }

            return new string(s);
        }

        internal static string ToKeyString(this MethodSymbol methodSymbol)
        {
            var sb = new StringBuilder();

            // return
            sb.Append(methodSymbol.ReturnType);
            sb.Append(" ");                

            var containingNamespaceOrType = methodSymbol.ContainingNamespaceOrType();
            if (containingNamespaceOrType != null)
            {
                sb.Append(containingNamespaceOrType);
                sb.Append(".");
            }

            sb.Append(methodSymbol.Name);
            if (methodSymbol.IsGenericMethod)
            {
                sb.Append("<");
                sb.Append(string.Join(",", methodSymbol.TypeParameters));
                sb.Append(">");
            }

            sb.Append("(");
            var any = false;
            if (methodSymbol.ParameterCount > 0)
            {
                foreach (var parameter in methodSymbol.Parameters)
                {
                    if (any)
                    {
                        sb.Append(", ");
                    }

                    if (parameter.RefKind.HasFlag(RefKind.Out))
                    {
                        sb.Append("out ");
                    }

                    if (parameter.RefKind.HasFlag(RefKind.Ref))
                    {
                        sb.Append("ref ");
                    }

                    sb.Append(parameter.Type);
                    any = true;
                }
            }

            sb.Append(")");

            return sb.ToString();
        }

        internal static string ToKeyString(this TypeSymbol typeSymbol)
        {
            var sb = new StringBuilder();

            var containingNamespaceOrType = typeSymbol.ContainingNamespaceOrType();
            if (containingNamespaceOrType != null)
            {
                if (containingNamespaceOrType.IsType)
                {
                    sb.Append(((TypeSymbol)containingNamespaceOrType).ToKeyString());
                }
                else
                {
                    sb.Append(containingNamespaceOrType);
                    sb.Append(".");
                }
            }

            sb.Append(typeSymbol.MetadataName);

            return sb.ToString();
        }

        public static string GetTypeName(this INamedTypeSymbol type)
        {
            if (type.ContainingType != null)
            {
                return type.ContainingType.GetTypeName() + "_" + type.MetadataName;
            }

            return type.MetadataName;
        }
    }
}