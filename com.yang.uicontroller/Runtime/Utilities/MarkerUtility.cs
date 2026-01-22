using System.Runtime.CompilerServices;

namespace Yang.UIController
{
    public static class MarkerUtility
    {
        public static bool ConvertMarker<T, U>(this T marker, out U result)
        {
            if (typeof(T) == typeof(U))
            {
                result = Unsafe.As<T, U>(ref marker);

                return true;
            }

            result = default;

            return false;
        }
    }

    public interface IDataMarker
    {
        public string MarkerID { get; }
    }
}