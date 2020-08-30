﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reinforced.Tecture.Testing.Data.SyntaxGeneration.Collection;
using Reinforced.Tecture.Testing.Validation;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Reinforced.Tecture.Testing.Data.SyntaxGeneration
{
    class Generator
    {
        private readonly TypeGeneratorRepository _tgr;

        private IEnumerable<PropertyInfo> GetProperties()
        {
            var props = TypeRef.GetProperties(BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.Instance);
            foreach (var propertyInfo in props)
            {
                if (typeof(LambdaExpression).IsAssignableFrom(propertyInfo.PropertyType))
                    continue;
                if (typeof(Task).IsAssignableFrom(propertyInfo.PropertyType))
                    continue;
                if (typeof(Delegate).IsAssignableFrom(propertyInfo.PropertyType))
                    continue;
                yield return propertyInfo;
            }

        }
        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public Generator(Type typeRef, TypeGeneratorRepository tgr)
        {
            TypeRef = typeRef;
            try
            {
                _defaultInstance = Activator.CreateInstance(typeRef);
            }
            catch (Exception ex)
            {
                _defaultInstance = typeRef.InstanceNonpublic();
            }

            var properties = GetProperties();
            InlineProperties = properties.Where(x => x.PropertyType.IsInlineable()).ToArray();
            CollectionProperties = properties.Where(x => x.PropertyType.IsEnumerable() || (x.PropertyType.IsTuple() && !x.PropertyType.IsInlineable())).ToArray();
            NestedProperties =
                properties.Where(x => !InlineProperties.Contains(x) && !CollectionProperties.Contains(x)).ToArray();

            foreach (var np in NestedProperties)
            {
                tgr.EnsureGeneratorFor(np.PropertyType);
            }

            _tgr = tgr;
        }

        public Type TypeRef { get; set; }

        public PropertyInfo[] InlineProperties { get; private set; }
        public PropertyInfo[] CollectionProperties { get; private set; }
        public PropertyInfo[] NestedProperties { get; private set; }

        private readonly object _defaultInstance;

        private string ExtractEnumUsing(Type t)
        {
            if (t.IsNullable())
            {
                t = Nullable.GetUnderlyingType(t);
            }

            if (t.IsEnum)
            {
                return t.Namespace;
            }

            return t.Namespace;
        }

        private List<StatementSyntax> ProduceInlineableProperties(string instanceName, object instance, GenerationContext context)
        {
            List<StatementSyntax> initNodes = new List<StatementSyntax>();

            foreach (var propertyInfo in InlineProperties)
            {
                var value = _tgr.Hijack.GetValue(instance, propertyInfo);
                var defValue = propertyInfo.GetValue(_defaultInstance);
                if (!Equals(defValue, value))
                {
                    var pName = propertyInfo.Name;
                    // "value"
                    var propValue = TypeInitConstructor.Construct(propertyInfo.PropertyType, value);

                    //Set<X,Y>
                    var setWithArguments = IdentifierName(nameof(CSharpTestData.Set));

                    var vName = "x";
                    var ident = IdentifierName(vName);
                    // x.Property
                    var memberAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ident, IdentifierName(pName));

                    // x => x.Property
                    var propLambda = SimpleLambdaExpression(Parameter(Identifier(vName)), memberAccess);

                    // instance, x=>x.Property, "value"
                    var arguments =
                        SeparatedList<ArgumentSyntax>(new[] { Argument(IdentifierName(instanceName)), Argument(propLambda), Argument(propValue) }
                            .ComaSeparated());

                    //Set<X,Y>(x=>x.Property,"value")
                    var invokation = InvocationExpression(setWithArguments)
                        .WithArgumentList(ArgumentList(arguments));

                    initNodes.Add(ExpressionStatement(invokation));
                    var u = ExtractEnumUsing(propertyInfo.PropertyType);
                    context.AddUsing(u);
                }
            }

            return initNodes;
        }

        private void ProduceNestedProperties(string instanceName, object instance, GenerationContext context)
        {

            foreach (var propertyInfo in NestedProperties)
            {
                var value = _tgr.Hijack.GetValue(instance, propertyInfo);
                var defValue = propertyInfo.GetValue(_defaultInstance);
                if (!Equals(defValue, value))
                {
                    var generator = _tgr.GetGeneratorFor(propertyInfo.PropertyType);

                    generator.New(value, context);
                    var varName = context.GetDefined(value);
                    
                    var ma = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(instanceName),
                        IdentifierName(propertyInfo.Name));

                    var ae = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        ma,
                        IdentifierName(varName)).WithTrailingTrivia(LineFeed);


                    context.LateBound.Enqueue(ExpressionStatement(ae));
                    context.AddUsing(propertyInfo.PropertyType.Namespace);
                }
            }
        }

        internal static ExpressionSyntax ProceedTuple(TypeGeneratorRepository tgr, IEnumerable<(Type, object)> values,
            GenerationContext context)
        {
            var variables = new List<ExpressionSyntax>();
            foreach (var item in values)
            {
                if (item.Item1.IsInlineable() || item.Item2 == null)
                {
                    variables.Add(TypeInitConstructor.Construct(item.Item1, item.Item2));
                }
                else
                {
                    var generator = tgr.GetGeneratorFor(item.Item1);
                    generator.New(item.Item2, context);
                    var name = context.GetDefined(item.Item2);
                    variables.Add(IdentifierName(name));
                }
            }

            var collectionStrategy = tgr.CollectionStrategies.GetTupleStrategy(values.Select(x => x.Item1));

            return collectionStrategy.Generate(variables, context.Usings);
        }
        internal static ExpressionSyntax ProceedCollection(TypeGeneratorRepository tgr, Type collectionType, IEnumerable values, GenerationContext context)
        {
            var generator = tgr.GetGeneratorFor(collectionType.ElementType());

            var variables = new List<ExpressionSyntax>();
            foreach (var item in values)
            {
                if (item == null)
                {
                    variables.Add(LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                else
                {
                    generator.New(item, context);
                    var name = context.GetDefined(item);
                    variables.Add(IdentifierName(name));
                }
            }

            var collectionStrategy = tgr.CollectionStrategies.GetStrategy(collectionType);
            
            return collectionStrategy.Generate(variables, context.Usings);
        }

        private void ProduceCollectionProperties(string instanceName, object instance, GenerationContext context)
        {
            List<ExpressionSyntax> initNodes = new List<ExpressionSyntax>();

            foreach (var propertyInfo in CollectionProperties)
            {
                var value = _tgr.Hijack.GetValue(instance, propertyInfo) as IEnumerable;
                var defValue = propertyInfo.GetValue(_defaultInstance);
                if (defValue != value)
                {

                    var collCreation =
                        propertyInfo.PropertyType.IsTuple()
                            ? ProceedTuple(_tgr, value.GetTupleValues(), context)
                            : ProceedCollection(_tgr, propertyInfo.PropertyType, value, context);

                    var ma = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(instanceName),
                        IdentifierName(propertyInfo.Name));

                    var ae = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        ma,
                        collCreation).WithTrailingTrivia(LineFeed);

                    context.LateBound.Enqueue(ExpressionStatement(ae));


                    context.AddUsing(propertyInfo.PropertyType.Namespace);
                }
            }
        }

        private static TypeSyntax Var
        {
            get
            {
                return IdentifierName("var");
            }
        }
        private ExpressionSyntax New(object instance, GenerationContext context)
        {
            if (instance == null)
                return LiteralExpression(SyntaxKind.NullLiteralExpression);

            var t = instance.GetType();
            if (t.IsEnumerable())
            {
                return ProceedCollection(_tgr, t, (IEnumerable)instance, context);
            }

            if (t.IsTuple())
            {
                if (t.IsInlineable()) return TypeInitConstructor.Construct(t, instance);
                return ProceedTuple(_tgr, instance.GetTupleValues(), context);
            }


            if (!context.DefinedVariable(instance, out string instanceName))
            {
                var initNodes = ProduceInlineableProperties(instanceName, instance, context);

                var tn = t.TypeName(context.Usings);
                var result = InvocationExpression(GenericName(nameof(CSharpTestData.New))
                    .WithTypeArgumentList(TypeArgumentList(
                        SingletonSeparatedList<TypeSyntax>(tn))));

                var vbl = SingletonSeparatedList<VariableDeclaratorSyntax>(
                    VariableDeclarator(Identifier(instanceName)
                    ).WithInitializer(EqualsValueClause(result)));

                var assign = LocalDeclarationStatement(VariableDeclaration(Var)
                    .WithVariables(vbl));

                context.Declarations.Enqueue(assign);
                foreach (var nd in initNodes)
                {
                    context.Declarations.Enqueue(nd);
                }

                ProduceNestedProperties(instanceName, instance, context);
                ProduceCollectionProperties(instanceName, instance, context);
            }

            return IdentifierName(instanceName);
        }

        public void Proceed(object instance, GenerationContext context)
        {
            var cre = New(instance, context);

            context.Declarations.Enqueue(ReturnStatement(cre));
        }
    }
}
