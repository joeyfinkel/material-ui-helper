using System;
using System.Collections.Generic;
using System.Threading;

namespace MaterialUiHelper.Tests.Helpers;

internal class TestDataGenerator<T> where T : class, new()
{
    // Use ThreadLocal<Random> for thread-safe random behavior
    private static readonly ThreadLocal<Random> ThreadRandom =
        new(() => new Random(Guid.NewGuid().GetHashCode()));

    private static Random Randomizer => ThreadRandom.Value!;

    /// <summary>
    /// Generates a list of <typeparamref name="T"/> objects.
    /// If no factory is provided, a default instance generator is used.
    /// </summary>
    /// <param name="count">Number of instances to generate.</param>
    /// <param name="factory">
    /// Optional callback that receives the index and should return a new instance of <typeparamref name="T"/>.
    /// </param>
    /// <returns>A list of generated objects.</returns>
    public static List<T> GenerateData(int count, Func<int, T>? factory = null)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        var list = new List<T>(count);

        for (var i = 0; i < count; i++)
        {
            var item = factory?.Invoke(i) ?? CreateDefaultInstance(i);

            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// Default instance creation if no factory is provided.
    /// Attempts shallow "random fill" for common property types.
    /// </summary>
    private static T CreateDefaultInstance(int index)
    {
        var instance = new T();
        var type = typeof(T);

        foreach (var property in type.GetProperties())
        {
            if (!property.CanWrite) continue;

            var propType = property.PropertyType;
            object? value = propType switch
            {
                not null when propType == typeof(string) => $"{property.Name}_{index}",
                not null when propType == typeof(int) => Randomizer.Next(1, 100),
                not null when propType == typeof(double) => Randomizer.NextDouble() * 100,
                not null when propType == typeof(DateTime) => DateTime.Now.AddDays(-Randomizer.Next(0, 1000)),
                not null when propType == typeof(bool) => Randomizer.Next(0, 2) == 0,
                _ => null
            };

            if (value != null)
            {
                property.SetValue(instance, value);
            }
        }

        return instance;
    }
}