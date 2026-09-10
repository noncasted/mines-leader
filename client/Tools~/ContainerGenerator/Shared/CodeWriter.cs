using System.Text;

namespace ContainerGenerator {
    internal sealed class CodeWriter {
        private readonly StringBuilder _buffer = new StringBuilder();
        private int _indent;

        public void AppendLine(string? value = null) {
            if (string.IsNullOrEmpty(value)) {
                _buffer.AppendLine();
                return;
            }

            _buffer.Append(' ', _indent * 4);
            _buffer.AppendLine(value);
        }

        public void BeginBlock() {
            AppendLine("{");
            _indent++;
        }

        public void EndBlock() {
            if (_indent > 0)
                _indent--;

            AppendLine("}");
        }

        public override string ToString() {
            return _buffer.ToString();
        }
    }
}
