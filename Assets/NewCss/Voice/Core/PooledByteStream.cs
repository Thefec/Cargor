using System;
using System.IO;

namespace NewCss.Voice.Core
{
    /// <summary>
    /// Önceden ayrılmış (havuzdan alınmış) sabit boyutlu bir byte[] üzerinde çalışan Stream.
    /// ASLA büyümez, ASLA yeni dizi ayırmaz — telsiz yakalama/çalma döngüsünde her frame'de
    /// aynı buffer <see cref="Reset"/> ile yeniden kullanılır.
    ///
    /// Neden MemoryStream değil: MemoryStream'i sabit bir byte[] ile (MemoryStream(buffer))
    /// kurarsan "genişleyemez" (not expandable) hâle gelir ve bu modda SetLength çağrısı
    /// NotSupportedException fırlatır. Steam'in ReadVoiceData(Stream)/DecompressVoice(Stream,int,Stream)
    /// API'leri verdiğimiz stream'i kendi akışlarında Write/SetLength ile kullanıyor; ses her
    /// yoklamada (30 Hz) exception yakalamak hem GC/try-catch maliyeti hem de ana döngüde
    /// öngörülemeyen dallanma demek. Bu yüzden kendi kontrolümüzdeki, exception fırlatmayan bir
    /// sözleşme kuruyoruz: kapasite aşımı sessizce kırpılır ve <see cref="Truncated"/> ile işaretlenir.
    /// </summary>
    public sealed class PooledByteStream : Stream
    {
        private readonly byte[] _buffer;
        private int _position;
        private int _length;

        /// <summary>
        /// Son Write/SetLength çağrısı kapasiteyi aştığı için veri kırpıldıysa true.
        /// Bir ses frame'ini burada kaybetmek, exception ile ana döngüyü kırmaktan daha iyidir —
        /// çağıran taraf isterse bunu sayaç/log ile takip eder, akış kesilmeden devam eder.
        /// </summary>
        public bool Truncated { get; private set; }

        /// <summary>Sabit buffer boyutu — bu asla değişmez (büyüme yok).</summary>
        public int Capacity => _buffer.Length;

        public PooledByteStream(byte[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        /// <summary>
        /// Position=0, Length=0, Truncated=false — buffer'ı sıfırdan kullanıma açar.
        /// Hiçbir ayırma yapmaz; alttaki byte[] fiziksel olarak aynı kalır (eski veri üzerine yazılır).
        /// </summary>
        public void Reset()
        {
            _position = 0;
            _length = 0;
            Truncated = false;
        }

        // Sabit dizi üzerinde çalıştığı için üçü de her zaman doğrudur.
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = ClampToCapacity(value);
        }

        /// <summary>No-op: gerçek bir I/O hedefi yok, tamponlanacak bir şey de yok.</summary>
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int available = _length - _position;
            if (available <= 0) return 0;

            int toCopy = Math.Min(count, available);
            Buffer.BlockCopy(_buffer, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    target = offset;
                    break;
                case SeekOrigin.Current:
                    target = _position + offset;
                    break;
                case SeekOrigin.End:
                    target = _length + offset;
                    break;
                default:
                    target = offset;
                    break;
            }

            _position = ClampToCapacity(target);
            return _position;
        }

        /// <summary>
        /// Sabit dizide "büyütme" imkânsız; istenen uzunluk kapasiteyi aşarsa kapasiteye kırpılır
        /// ve <see cref="Truncated"/> işaretlenir — NotSupportedException YOK (bkz. sınıf yorumu:
        /// Steam API'lerinin bu stream üzerinde exception'sız çalışması gerekiyor).
        /// </summary>
        public override void SetLength(long value)
        {
            if (value < 0) value = 0;

            if (value > Capacity)
            {
                _length = Capacity;
                Truncated = true;
            }
            else
            {
                _length = (int)value;
            }

            if (_position > _length) _position = _length;
        }

        /// <summary>
        /// Kapasiteyi aşan kısım YAZILMAZ ve exception ATILMAZ; sadece <see cref="Truncated"/>
        /// işaretlenir. Bir ses frame'ini burada kaybetmek, ana döngüyü kırmaktan iyidir.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            int spaceLeft = Capacity - _position;
            int toWrite = count;
            if (toWrite > spaceLeft)
            {
                toWrite = Math.Max(0, spaceLeft);
                Truncated = true;
            }

            if (toWrite > 0)
            {
                Buffer.BlockCopy(buffer, offset, _buffer, _position, toWrite);
                _position += toWrite;
                if (_position > _length) _length = _position;
            }
        }

        private int ClampToCapacity(long value)
        {
            if (value < 0) return 0;
            if (value > Capacity) return Capacity;
            return (int)value;
        }
    }
}
