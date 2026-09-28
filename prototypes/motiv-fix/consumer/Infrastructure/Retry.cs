namespace Consumer.Infrastructure;

public static class Retry
{
    public static bool ShouldRetry(int attempt, int max) => attempt < max;
}

public static class Pair
{
    public static bool Both(int a, int b) { var x = a > 1; var y = b > 2; return x & y; }
}
