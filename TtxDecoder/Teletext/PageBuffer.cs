namespace TtxDecoder.Teletext
{
    internal class PageBuffer
    {
        private readonly char[][] _buffer = {
            new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40], new char[40]
        };

        public void SetChar(int x, int y, char c)
        {
            _buffer[y][x] = c;
            _changed = true;
        }

        public char GetChar(int x, int y)
        {
            return _buffer[y][x];
        }

        private bool _changed;

        public bool IsChanged()
        {
            return _changed;
        }

        public void Clear()
        {
            foreach (var t in _buffer)
            {
                for (var x = 0; x < t.Length; x++)
                {
                    t[x] = '\0';
                }
            }
            _changed = false;
        }
    }
}