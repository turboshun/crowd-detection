using System;
using System.Diagnostics;

namespace CrowdDetection
{
    /// <summary>
    /// 自動感度の取得を行う
    /// </summary>
    internal class SearchAutoSensitivity
    {
        /// <summary>
        /// GetAutoSensitivity()の戻り値、自動感度取得状況。
        /// </summary>
        public enum GetAutoSensitivityReturnID
        {
            /// <summary>処理が開始されていない</summary>
            NoStart = -2,
            /// <summary>エラー</summary>
            Error,
            /// <summary>成功</summary>
            Success,
            /// <summary>継続中</summary>
            Continue,
        }

        /// <summary>
        /// 感度自動設定の待ち時間(ms)
        /// </summary>
        private const int SENSITIVITY_WAITTIME_FIRST = 2000;
        /// <summary>
        /// 感度自動設定の限界待ち時間(ms)
        /// </summary>
        private const int SENSITIVITY_WAITTIME_LIMIT = 10000;

        /// <summary>
        /// 感度の補正値
        /// </summary>
        private const int THRESHOLD_OFFSET = 5;
        /// <summary>
        /// 感度自動設定で必要な値の数
        /// </summary>
        private const int AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT = 5;

        /// <summary>
        /// 処理を開始しているか(動作中か)？
        /// </summary>
        private bool _isStart;
        /// <summary>
        /// 感度値の最大を取得した回数
        /// </summary>
        private int _maxSensitivityGetCount;
        /// <summary>
        /// タイマー
        /// </summary>
        private Stopwatch _countTimer = new Stopwatch();
        /// <summary>
        /// タイマー
        /// </summary>
        private int[] _autosenceNeedMaxSensitivities = new int[AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT];

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SearchAutoSensitivity()
        {
            Clear();
        }

        /// <summary>
        /// 値のクリア
        /// </summary>
        private void Clear()
        {
            _isStart = false;
            _maxSensitivityGetCount = 0;
            _countTimer.Reset();

            for (int i = 0; i < _autosenceNeedMaxSensitivities.Length; i++)
            {
                _autosenceNeedMaxSensitivities[i] = -1;
            }
        }

        /// <summary>
        /// 自動感度取得開始
        /// </summary>
        public bool StartCalcAutoSensitivity()
        {
            if (_isStart == true)
            {
                return false;
            }

            _maxSensitivityGetCount = 0;

            for (int i = 0; i < _autosenceNeedMaxSensitivities.Length; i++)
            {
                _autosenceNeedMaxSensitivities[i] = -1;
            }

            _isStart = true;
            _countTimer.Restart();

            return true;
        }

        /// <summary>
        /// 自動感度を取得する
        /// </summary>
        /// <param name="sensitivity">感度</param>
        /// <returns>GetAutoSensitivityReturnID:自動感度取得状況</returns>
        public GetAutoSensitivityReturnID GetAutoSensitivity(out int sensitivity)
        {
            sensitivity = 0;

            if (_isStart == false)
            {
                // 感度自動設定を行っていない
                return GetAutoSensitivityReturnID.NoStart;
            }

            int span = (int)_countTimer.ElapsedMilliseconds;
            if (span < SENSITIVITY_WAITTIME_FIRST)
            {
                return GetAutoSensitivityReturnID.Continue;
            }

            if (_maxSensitivityGetCount < AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT)
            {
                // 10秒間で感度用データが指定カウント取得できなければ自動設定失敗
                return (span >= SENSITIVITY_WAITTIME_LIMIT ? GetAutoSensitivityReturnID.Error : GetAutoSensitivityReturnID.Continue);
            }

            int total = 0;
            for (int i = 1; i < AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT; i++)
            {
                total += _autosenceNeedMaxSensitivities[i];
            }
            sensitivity = total / (AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT - 1);

            sensitivity += THRESHOLD_OFFSET;
            sensitivity = Math.Max(sensitivity, CrowdDetection.SENSITIVITY_MINIMUM);
            sensitivity = Math.Min(sensitivity, CrowdDetection.SENSITIVITY_MAXIMUM);

            Clear();

            return GetAutoSensitivityReturnID.Success;    // 感度自動設定終了
        }

        /// <summary>
        /// 感度値の最大を設定する
        /// </summary>
        /// <param name="maxSensitivity">感度値の最大</param>
        /// <returns>true:感度自動設定を行う条件を満たした false:感度自動設定を行う条件を満たしていない</returns>
        public bool SetMaxSensitivity(int maxSensitivity)
        {
            if (_isStart == false ||    // 感度自動設定中でない
                maxSensitivity < 0 ||   // 引数が不正
                _autosenceNeedMaxSensitivities[AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT - 1] > maxSensitivity)  // リストの最小の値より小さい
            {
                return false;
            }

            if (_maxSensitivityGetCount < AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT)
            {
                _maxSensitivityGetCount++;
            }

            for (int i = 0; i < AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT; i++)
            {
                if (_autosenceNeedMaxSensitivities[i] < maxSensitivity)
                {
                    for (int j = _maxSensitivityGetCount - 1; j > i; j--)
                    {
                        _autosenceNeedMaxSensitivities[j] = _autosenceNeedMaxSensitivities[j - 1];
                    }
                    _autosenceNeedMaxSensitivities[i] = maxSensitivity;
                    break;
                }
            }

            return (_maxSensitivityGetCount >= AUTOSENSE_NEED_MAX_SENSITIVITY_GET_COUNT && _countTimer.ElapsedMilliseconds > SENSITIVITY_WAITTIME_FIRST);
        }
    }
}
