namespace DynamicODataToSQL.Test;

public static class Utility
{
    public static bool DictionariesAreEqual(IDictionary<string, object> d1, IDictionary<string, object> d2)
    {
        // If any keys are missing/extra/different, the dicts are not the same
        if (!d1.Keys.ToHashSet().SetEquals(d2.Keys.ToHashSet()))
        {
            return false;
        }

        foreach (var key in d1.Keys)
        {
            var v1 = d1[key];
            var v2 = d2[key];
            if (!Equals(v1, v2))
            {
                return false;
            }
        }

        return true;
        // Next, count the differences between the corresponding values
        //return d1.All(kv => d2.TryGetValue(kv.Key, out var value) && Equals(kv.Value, value));
    }
}
