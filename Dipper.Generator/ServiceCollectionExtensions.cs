using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dipper.Generator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDataAnnotationsSupport(this IServiceCollection services,
        IEnumerable<Assembly>? assemblies = null)
    {
        IEnumerable<Type> types = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies())
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
            {
                PropertyInfo[] props = type.GetProperties();
                return props.Any(info => info.GetCustomAttribute<ColumnAttribute>() is not null);
            });

        foreach (Type type in types)
        {
            Dapper.SqlMapper.SetTypeMap(
                type,
                new Dapper.CustomPropertyTypeMap(
                    type,
                    (type, columnName) =>
                    {
                        var propertyInfo = type.GetProperties()
                            .FirstOrDefault(prop =>
                                prop.GetCustomAttributes(false)
                                    .OfType<ColumnAttribute>()
                                    .Any(attr => attr.Name == columnName));

                        return propertyInfo;
                    }
                ));
        }


        return services;
    }
}