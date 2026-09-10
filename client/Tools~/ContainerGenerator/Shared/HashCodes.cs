namespace ContainerGenerator {
    internal static class HashCodes {
        public static int Combine(int a, int b) {
            return unchecked((a * 397) ^ b);
        }

        public static int Combine(int a, int b, int c) {
            return Combine(Combine(a, b), c);
        }

        public static int Combine(int a, int b, int c, int d) {
            return Combine(Combine(a, b, c), d);
        }

        public static int Of(string? value) {
            return value == null ? 0 : value.GetHashCode();
        }

        public static int Of(bool value) {
            return value ? 1 : 0;
        }
    }
}
