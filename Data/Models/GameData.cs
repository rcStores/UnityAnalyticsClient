namespace Advant.Data.Models
{
    internal enum EValueType
    {
        Int, Float, Double, String, Bool, DateTime
    }

    //public struct Value
    //{
    //    string data;
    //    EValueType type;

    //    int AsInt() { return Convert.ToInt32(data); }
    //    double AsDouble() { return Convert.ToDouble(data); }
    //    string AsString() { return data; }
    //    bool AsBool() { return Convert.ToBoolean(data); }
    //    DateTime AsDateTime() { return Convert.ToDateTime(data); }

    //    TResult CastToNativeType<TResult> ()
    //    {

    //        MethodInfo method = typeof(Value).GetMethod(nameof(Value.CastToNativeType));
    //        MethodInfo generic = method.MakeGenericMethod(int);
    //        generic.Invoke(this, null);
    //    }
    //}

    //public class EventParameters : Dictionary<string, Value>
    //{

    //}

    // will probably remove this
    //public class PropertyBase : GameData
    //{
    //    public string name { get; set; }
    //}
}
