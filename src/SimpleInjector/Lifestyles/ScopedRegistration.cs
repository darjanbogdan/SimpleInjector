﻿// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Lifestyles
{
    using System;
    using System.Linq.Expressions;

    internal abstract class ScopedRegistration : Registration
    {
        private Func<Scope?>? scopeFactory;

        internal ScopedRegistration(
            ScopedLifestyle lifestyle, Container container, Type implementationType)
            : base(lifestyle, container)
        {
            this.ImplementationType = implementationType;
        }

        public override Type ImplementationType { get; }
        public new ScopedLifestyle Lifestyle => (ScopedLifestyle)base.Lifestyle;

        // Initialized when BuildExpression is called
        internal Func<object>? InstanceCreator { get; private set; }

        public override Expression BuildExpression()
        {
            if (this.InstanceCreator is null)
            {
                this.scopeFactory = this.Lifestyle.CreateCurrentScopeProvider(this.Container);

                this.InstanceCreator = this.BuildInstanceCreator();
            }

            return Expression.Call(
                instance: Expression.Constant(this),
                method: this.GetType().GetMethod(nameof(this.GetInstance))
                    .MakeGenericMethod(this.ImplementationType));
        }

        internal static Registration? GetScopedRegistration(MethodCallExpression node) =>
            node.Object is ConstantExpression instance
                ? instance.Value as ScopedRegistration
                : null;

        // This method needs to be public, because the BuildExpression methods build a
        // MethodCallExpression using this method, and this would fail in partial trust when the
        // method is not public.
        // Simple Injector does some aggressive optimizations for scoped lifestyles and this method will
        // is most cases not be called. It will however be called when the expression that is built by
        // this instance will get compiled by someone else than the core library. That's why this method
        // is still important.
        public TImplementation GetInstance<TImplementation>()
            where TImplementation : class =>
            Scope.GetInstance<TImplementation>(this, this.scopeFactory!());

        protected abstract Func<object> BuildInstanceCreator();
    }

    internal sealed class AutoWiredScopedRegistration : ScopedRegistration
    {
        internal AutoWiredScopedRegistration(
            ScopedLifestyle lifestyle, Container container, Type implementationType)
            : base(lifestyle, container, implementationType)
        {
        }

        protected override Func<object> BuildInstanceCreator() => this.BuildTransientDelegate();
    }

    internal sealed class DelegateScopedRegistration<TImplementation> : ScopedRegistration
        where TImplementation : class
    {
        private readonly Func<TImplementation> userSuppliedInstanceCreator;

        internal DelegateScopedRegistration(
            ScopedLifestyle lifestyle, Container container, Func<TImplementation> instanceCreator)
            : base(lifestyle, container, typeof(TImplementation))
        {
            this.userSuppliedInstanceCreator = instanceCreator;
        }

        protected override Func<object> BuildInstanceCreator() =>
            this.BuildTransientDelegate(this.userSuppliedInstanceCreator);
    }
}