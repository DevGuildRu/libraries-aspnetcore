﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevGuild.AspNetCore.Services.Permissions.Models;
using DevGuild.AspNetCore.Services.Permissions.Override;

namespace DevGuild.AspNetCore.Services.Permissions.Base
{
    /// <summary>
    /// Base implementation of the permissions manager.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <seealso cref="IPermissionsManager" />
    public abstract class BasePermissionsManager<TConfig> : IPermissionsManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BasePermissionsManager{TConfig}"/> class.
        /// </summary>
        /// <param name="hub">The permissions hub.</param>
        /// <param name="parentManager">The parent permissions manager.</param>
        /// <param name="permissionsNamespace">The permissions namespace.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="configuration">The manager configuration.</param>
        protected BasePermissionsManager(IPermissionsHub hub, ICorePermissionsManager parentManager, PermissionsNamespace permissionsNamespace, IServiceProvider serviceProvider, TConfig configuration)
        {
            this.Hub = hub;
            this.ParentManager = parentManager;
            this.PermissionsNamespace = permissionsNamespace;
            this.ServiceProvider = serviceProvider;
            this.Configuration = configuration;
        }

        /// <inheritdoc />
        public IPermissionsHub Hub { get; }

        /// <inheritdoc />
        public ICorePermissionsManager ParentManager { get; }

        /// <inheritdoc />
        public PermissionsNamespace PermissionsNamespace { get; }

        /// <summary>
        /// Gets the service provider.
        /// </summary>
        /// <value>
        /// The service provider.
        /// </value>
        protected IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Gets the manager configuration.
        /// </summary>
        /// <value>
        /// The manager configuration.
        /// </value>
        protected TConfig Configuration { get; }

        /// <inheritdoc />
        public virtual async Task<PermissionsResult> CheckPermissionAsync(Permission permission)
        {
            this.ValidatePermissionSupport(permission);

            if (this.Configuration is IOverridablePermissionsManagerConfiguration overridable && overridable.OverrideMode == PermissionsOverrideMode.BeforeChildCheck)
            {
                var overrideResult = await this.CheckPermissionOverrideAsync(permission);
                if (overrideResult != PermissionsResult.Undefined)
                {
                    return overrideResult;
                }
            }

            var explicitResult = await this.CheckExplicitPermissionAsync(permission);
            if (explicitResult != PermissionsResult.Undefined)
            {
                return explicitResult;
            }

            if (this.Configuration is IOverridablePermissionsManagerConfiguration overridable2 && overridable2.OverrideMode == PermissionsOverrideMode.AfterChildCheck)
            {
                var overrideResult = await this.CheckPermissionOverrideAsync(permission);
                if (overrideResult != PermissionsResult.Undefined)
                {
                    return overrideResult;
                }
            }

            return PermissionsResult.Undefined;
        }

        /// <inheritdoc />
        public virtual async Task<PermissionsResult> CheckPermissionsAsync(IEnumerable<Permission> permissions)
        {
            // That should be sufficient for now. Should implement more optimally later.
            var permissionsList = permissions.ToList();
            this.ValidatePermissionsSupport(permissionsList);

            foreach (var permission in permissionsList)
            {
                var individualResult = await this.CheckPermissionAsync(permission);
                if (individualResult == PermissionsResult.Undefined)
                {
                    return PermissionsResult.Undefined;
                }
            }

            return PermissionsResult.Allow;
        }

        /// <inheritdoc />
        public virtual async Task DemandPermissionAsync(Permission permission)
        {
            var check = await this.CheckPermissionAsync(permission);
            if (check != PermissionsResult.Allow)
            {
                throw await this.Hub.CreateUnauthorizedExceptionAsync();
            }
        }

        /// <inheritdoc />
        public async Task DemandPermissionsAsync(IEnumerable<Permission> permissions)
        {
            var check = await this.CheckPermissionsAsync(permissions);
            if (check != PermissionsResult.Allow)
            {
                throw await this.Hub.CreateUnauthorizedExceptionAsync();
            }
        }

        /// <summary>
        /// Asynchronously performs the explicit permissions check.
        /// </summary>
        /// <param name="permission">The checked permission.</param>
        /// <returns>A result of permissions check.</returns>
        protected abstract Task<PermissionsResult> CheckExplicitPermissionAsync(Permission permission);

        /// <summary>
        /// Validates that specified permission is supported.
        /// </summary>
        /// <param name="permission">The validated permission.</param>
        /// <exception cref="InvalidOperationException">Permission is not defined in namespace.</exception>
        protected void ValidatePermissionSupport(Permission permission)
        {
            if (!this.PermissionsNamespace.IsDefined(permission))
            {
                throw new InvalidOperationException($"Permission {permission.Name} is not defined in namespace {this.PermissionsNamespace.Name}");
            }
        }

        /// <summary>
        /// Validates that specified permissions are supported.
        /// </summary>
        /// <param name="permissions">The validated permissions.</param>
        /// <exception cref="InvalidOperationException">Permissions is not defined in namespace.</exception>
        protected void ValidatePermissionsSupport(IEnumerable<Permission> permissions)
        {
            foreach (var permission in permissions)
            {
                if (!this.PermissionsNamespace.IsDefined(permission))
                {
                    throw new InvalidOperationException($"Permission {permission.Name} is not defined in namespace {this.PermissionsNamespace.Name}");
                }
            }
        }

        /// <summary>
        /// Asynchronously checks the permission override.
        /// </summary>
        /// <param name="permission">The checked permission.</param>
        /// <returns>A result of permissions check.</returns>
        protected virtual async Task<PermissionsResult> CheckPermissionOverrideAsync(Permission permission)
        {
            if (this.Configuration is IOverridablePermissionsManagerConfiguration overridable && this.ParentManager is IPermissionsManager parentManager)
            {
                var overrides = overridable.GetOverridesForPermission(permission);
                foreach (var @override in overrides)
                {
                    var parentResult = await parentManager.CheckPermissionsAsync(@override.OverridingPermissions);
                    if (parentResult != PermissionsResult.Undefined)
                    {
                        return parentResult;
                    }
                }
            }

            return PermissionsResult.Undefined;
        }
    }
}
