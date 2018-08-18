namespace CrowdDetection
{
    /// <summary>
    /// 動き検知処理クラス
    /// </summary>
    public class CrowdDetection
    {
        /// <summary>
        /// 画像データ、動き検知情報の管理
        /// </summary>
        private ImageDataManager _imageDataManager = new ImageDataManager();

        /// <summary>
        /// 自動感度の取得
        /// </summary>
        private SearchAutoSensitivity _searchAutoSensitivity = new SearchAutoSensitivity();

        /// <summary>
        /// 動き検知計算処理完了通知用デリゲート
        /// </summary>
        /// <param name="jpegImageData">検知対象の画像データ</param>
        /// <param name="orgJpegImageData">オリジナルのの画像データ</param>
        public delegate void CopmleteCalcCroudDetectionProcImageDelegate(byte[] jpegImageData, byte[] orgJpegImageData = null);
        /// <summary>
        /// 動き検知計算処理完了通知
        /// </summary>
        public CopmleteCalcCroudDetectionProcImageDelegate CopmleteCalcCroudDetectionProcImage = null;

        /// <summary>
        /// 動き検知計算処理完了通知用デリゲート
        /// </summary>
        /// <param name="jpegImageData">検知対象の画像データ</param>
        /// <param name="cabbBuffer">検知領域差分用バッファ</param>
        /// <param name="sensitivityThreshold">検知感度閾値</param>
        /// <param name="orgJpegImageData">オリジナルのの画像データ</param>
        public delegate void CopmleteCalcCroudDetectionProcDetectInfoDelegate(byte[] jpegImageData, int[] cabbBuffer, int sensitivityThreshold, byte[] orgJpegImageData = null);
        /// <summary>
        /// 動き検知計算処理完了通知
        /// </summary>
        public CopmleteCalcCroudDetectionProcDetectInfoDelegate CopmleteCalcCroudDetectionProcDetectInfo = null;

        /// <summary>
        /// 動き検知計算処理完了通知用デリゲート
        /// </summary>
        /// <param name="detectedArea">現在の検知領域(範囲、面積比)</param>
        public delegate void CopmleteCalcCroudDetectionProcDetectedAreaDelegate(double detectedArea);
        /// <summary>
        /// 動き検知計算処理完了通知
        /// </summary>
        public CopmleteCalcCroudDetectionProcDetectedAreaDelegate CopmleteCalcCroudDetectionProcDetectedArea = null;

        /// <summary>
        /// 自動感度取得処理完了通知用デリゲート
        /// </summary>
        /// <param name="sensitivity">感度</param>
        public delegate void CopmleteCalcAutoSensitivityDelegate(int sensitivity);
        /// <summary>
        /// 自動感度取得処理完了通知
        /// </summary>
        public CopmleteCalcAutoSensitivityDelegate CopmleteCalcAutoSensitivity = null;

        /// <summary>
        /// 感度最大値
        /// </summary>
        public const int SENSITIVITY_MAXIMUM = 256;
        /// <summary>
        /// 感度最小値
        /// </summary>
        public const int SENSITIVITY_MINIMUM = 1;
        /// <summary>
        /// 感度 デフォルト値
        /// </summary>
        public const int SENSITIVITY_DEFAULT = 1;
        /// <summary>
        /// 検知領域(範囲、面積比)最大
        /// </summary>
        public const double DETECTED_AREA_MAXIMUM = 100.0;
        /// <summary>
        /// 検知領域(範囲、面積比)最小
        /// </summary>
        public const double DETECTED_AREA_MINIMUM = 0.0;
        /// <summary>
        /// 検知領域(範囲、面積比)閾値デフォルト
        /// </summary>
        public const double DETECTED_AREA_THRESHOLD_DEFAULT = 10.0;

        /// <summary>
        /// 検知感度を取得または設定する
        /// </summary>
        public int Sensitivity
        {
            get { return SENSITIVITY_MAXIMUM + SENSITIVITY_MINIMUM - _imageDataManager.Sensitivity; }
            set { _imageDataManager.Sensitivity = SENSITIVITY_MAXIMUM + SENSITIVITY_MINIMUM - value; }
        }

        /// <summary>
        /// 検知領域(範囲、面積比)閾値を取得または設定する
        /// </summary>
        public double DetectedAreaThreshold { get; set; } = DETECTED_AREA_THRESHOLD_DEFAULT;

        /// <summary>
        /// 現在の検知ブロック数
        /// </summary>
        private int _currentDetectBlockCount = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CrowdDetection()
        {
            Sensitivity = SENSITIVITY_DEFAULT;
            DetectedAreaThreshold = DETECTED_AREA_THRESHOLD_DEFAULT;
        }

        /// <summary>
        /// 動き検知計算処理
        /// </summary>
        /// <param name="jpegImageData">検知計算対象のJPEGデータ</param>
        /// <param name="orgJpegImageData">オリジナルJPEGデータ</param>
        public void CalcCroudDetectionProc(byte[] jpegImageData, byte[] orgJpegImageData = null)
        {
            _imageDataManager.JpegImageData = jpegImageData;

            bool ret = _imageDataManager.CalcInterframeDifference(ref _currentDetectBlockCount);

            CopmleteCalcCroudDetectionProcImage?.Invoke(
                ret ? _imageDataManager.JpegImageData : null,
                ret ? orgJpegImageData : null);
            CopmleteCalcCroudDetectionProcDetectInfo?.Invoke(
                ret ? _imageDataManager.JpegImageData : null,
                _imageDataManager.CabbBuffer,
                _imageDataManager.Sensitivity,
                ret ? orgJpegImageData : null);
            CopmleteCalcCroudDetectionProcDetectedArea?.Invoke(
                ret ? ((double)_currentDetectBlockCount / _imageDataManager.CabbBuffer.Length) * DETECTED_AREA_MAXIMUM : 0);

            if (ret)
            {
                // 自動感度取得処理
                if (_searchAutoSensitivity.SetMaxSensitivity(_imageDataManager.DetectMaxSensitivity))
                {
                    switch (_searchAutoSensitivity.GetAutoSensitivity(out int sensitivity))
                    {
                        case SearchAutoSensitivity.GetAutoSensitivityReturnID.Success:  // 感度自動設定成功
                            CopmleteCalcAutoSensitivity?.Invoke(sensitivity);
                            break;
                        case SearchAutoSensitivity.GetAutoSensitivityReturnID.Error:    // エラー
                            CopmleteCalcAutoSensitivity?.Invoke(0);
                            break;
                        default:    // 処理を続行
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 自動感度取得開始
        /// </summary>
        public bool StartCalcAutoSensitivity()
        {
            return _searchAutoSensitivity.StartCalcAutoSensitivity();
        }
    }
}
