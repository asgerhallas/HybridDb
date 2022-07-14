namespace HybridDb
{
    public static class HybridDbParametersEx
    {
        public static HybridDbParameters ToHybridDbParameters(this object parameters)
        {
            if (parameters is HybridDbParameters hybridDbParameters) return hybridDbParameters;

            var result = new HybridDbParameters();
            result.Add(parameters);

            return result;
        }
    }
}