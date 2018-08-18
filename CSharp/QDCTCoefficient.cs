namespace CrowdDetection
{
    /// <summary>
    /// 量子化後のDCT係数列等の取得、管理
    /// </summary>
    internal partial class QDCTCoefficient
    {
        /// <summary>
        /// 量子化後のDCT係数列
        /// </summary>
        private class QDCTCoefficientParameter
        {
            /// <summary> 画像の幅 </summary>
            public int Width { get; set; } = 0;
            /// <summary> 画像の高さ </summary>
            public int Height { get; set; } = 0;
            /// <summary> 水平方向の8x8ブロック数 </summary>
            public int BlockNumX { get; set; } = 0;
            /// <summary> 垂直方向の8x8ブロック数 </summary>
            public int BlockNumY { get; set; } = 0;
            /// <summary>
            /// 輝度(Y)成分の量子化後のDCT係数列
            /// </summary>
            /// <remarks>DCT係数はジグザグスキャン順に64個並び、これがラスタスキャン順にブロック数分だけ繰り返す。</remarks>
            public short[] YComponents { get; set; } = null;

            /// <summary>
            /// 値をコピーする
            /// </summary>
            /// <param name="qdctCoeffParam">コピー先</param>
            public void Copy(ref QDCTCoefficientParameter qdctCoeffParam)
            {
                if (qdctCoeffParam != null)
                {
                    qdctCoeffParam.Width = Width;
                    qdctCoeffParam.Height = Height;
                    qdctCoeffParam.BlockNumX = BlockNumX;
                    qdctCoeffParam.BlockNumY = BlockNumY;
                    if (YComponents != null)
                    {
                        qdctCoeffParam.YComponents = (YComponents == null ? null : (short[])YComponents.Clone());
                    }
                }
            }
        }

        /// <summary>
        /// 量子化後のDCT係数列
        /// </summary>
        private QDCTCoefficientParameter _qdctCoeffParam = null;

        /// <summary>
        /// Q Factor
        /// </summary>
        private ushort _qFactor = 0;

        /// <summary>
        /// JPEGスキャニング
        /// </summary>
        private ScanJpeg _scanJpeg = new ScanJpeg();

        /// <summary>
        /// SjGetQuantizedDCTCoeff()を実行
        /// </summary>
        /// <param name="jpegImageData">JPEGデータ</param>
        /// <returns>true:成功、false:失敗</returns>
        public bool SetJpegToDCTCoeff(byte[] jpegImageData)
        {
            if (jpegImageData == null)
            {
                return false;
            }

            return (_scanJpeg.GetQuantizedDCTCoeff(jpegImageData, out _qdctCoeffParam, out _qFactor) && _qdctCoeffParam.Width > 0 && _qdctCoeffParam.Height > 0);
        }

        /// <summary>
        /// 映像サイズを取得する
        /// </summary>
        /// <param name="imageWidth">イメージ幅</param>
        /// <param name="imageHeight">イメージ高さ</param>
        public void GetImageSize(ref int imageWidth, ref int imageHeight)
        {
            imageWidth = (_qdctCoeffParam == null ? 0 : _qdctCoeffParam.Width);
            imageHeight = (_qdctCoeffParam == null ? 0 : _qdctCoeffParam.Height);
        }

        /// <summary>
        /// 8x8ブロック数を取得
        /// </summary>
        /// <param name="blockNumX">水平方向の8x8ブロック数</param>
        /// <param name="blockNumY">垂直方向の8x8ブロック数</param>
        public void GetBlockNumber(ref int blockNumX, ref int blockNumY)
        {
            blockNumX = (_qdctCoeffParam == null ? 0 : _qdctCoeffParam.BlockNumX);
            blockNumY = (_qdctCoeffParam == null ? 0 : _qdctCoeffParam.BlockNumY);
        }

        /// <summary>
        /// Q Factorを取得
        /// </summary>
        /// <returns>ushort:Q Factor</returns>
        public ushort GetQFactor()
        {
            return _qFactor;
        }

        /// <summary>
        /// 輝度(Y)成分の量子化後のDCT係数列を取得
        /// </summary>
        /// <returns>short[]:輝度(Y)成分の量子化後のDCT係数列</returns>
        public short[] GethQDCT_COEFF()
        {
            return _qdctCoeffParam?.YComponents;
        }
    }
}
