using System;

namespace DynamicBird.Core.Controllers
{
    /// <summary>
    /// 边缘触发时序状态机（防抖 / 触发延时 / 快速切换计数）。
    /// 纯逻辑、无 UI 依赖；时钟可注入（IClock），便于单元测试时间行为。
    /// 从 EdgeTriggerController 提取，控制器委托本类判定。
    /// </summary>
    public sealed class EdgeTimingState
    {
        /// <summary>时钟抽象：生产用 SystemClock，测试可注入假时钟。</summary>
        public interface IClock { DateTime Now { get; } }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }

        private readonly IClock _clock;
        private readonly Func<string, int> _getTriggerDelay;

        // ===== 防抖 =====
        private EdgeRegion _lastDebounceRegion = EdgeRegion.Unknown;
        private DateTime _lastRegionChangeTime = DateTime.MinValue;

        // ===== 触发延时 =====
        private string _delayRegionKey = "";
        private string _delayEdge = "";
        private DateTime _delayEnterTime = DateTime.MinValue;
        private bool _triggerDelaying;

        // ===== 快速切换 =====
        private const double RapidSwitchWindowMs = 1000;
        private int _switchCount = 0;
        private DateTime _lastSwitchTime = DateTime.MinValue;

        public EdgeTimingState(IClock? clock = null, Func<string, int>? getTriggerDelay = null)
        {
            _clock = clock ?? new SystemClock();
            _getTriggerDelay = getTriggerDelay ?? (_ => 0);
        }

        public bool IsTriggerDelaying => _triggerDelaying;

        // ========== 防抖 ==========

        /// <summary>区域快速抖动过滤（ProcessRegion 语义）：同一区域在防抖窗口内重复出现 → 丢弃；
        /// 放行时不刷新时间戳（下次同区域判定仍以首次进入时刻为准）。返回 true = 应丢弃本次。</summary>
        public bool ShouldDebounce(EdgeRegion region, double debounceMs)
        {
            if (_lastDebounceRegion != EdgeRegion.Unknown && _lastDebounceRegion == region)
            {
                if ((_clock.Now - _lastRegionChangeTime).TotalMilliseconds < debounceMs) return true;
            }
            else
            {
                _lastDebounceRegion = region;
                _lastRegionChangeTime = _clock.Now;
            }
            return false;
        }

        /// <summary>区域快速抖动过滤（FollowMouseInPanel 语义）：无论是否丢弃都刷新时间戳
        /// （每次类型变化都重新起算防抖窗口）。返回 true = 应丢弃本次。</summary>
        public bool ShouldDebounceAndRefresh(EdgeRegion region, double debounceMs)
        {
            bool drop = _lastDebounceRegion == region &&
                        (_clock.Now - _lastRegionChangeTime).TotalMilliseconds < debounceMs;
            _lastDebounceRegion = region;
            _lastRegionChangeTime = _clock.Now;
            return drop;
        }

        // ========== 触发延时 ==========

        /// <summary>
        /// 触发延时判定（面板隐藏时）：鼠标进入区域需停留 N ms 才放行（防误触）。
        /// 区域变化会重新计时；返回 true = 放行显示。region 由键+边标识（区域键足够，边冗余防误判）。
        /// </summary>
        public bool TriggerDelayPassed(EdgeRegion region)
        {
            string key = EdgeRegionMapping.GetRegionKey(region);
            string edge = EdgeRegionMapping.GetEdgeName(region);
            int delay = _getTriggerDelay(key);

            if (delay <= 0)
            {
                _delayRegionKey = key;
                _delayEdge = edge;
                _triggerDelaying = false;
                return true;
            }

            if (_delayRegionKey != key || _delayEdge != edge)
            {
                // 进入新区域：开始计时，本次不触发
                _delayRegionKey = key;
                _delayEdge = edge;
                _delayEnterTime = _clock.Now;
                _triggerDelaying = true;
                return false;
            }

            if ((_clock.Now - _delayEnterTime).TotalMilliseconds < delay)
            {
                _triggerDelaying = true;
                return false;
            }

            _triggerDelaying = false;
            return true;
        }

        /// <summary>鼠标离开边缘区域时重置触发延时计时（重新进入需重新停留）。</summary>
        public void ResetTriggerDelay()
        {
            _delayRegionKey = "";
            _delayEdge = "";
            _delayEnterTime = DateTime.MinValue;
            _triggerDelaying = false;
        }

        /// <summary>重置防抖记录（区域清空后调用；下次进入任意区域重新计时）。</summary>
        public void ResetDebounce()
        {
            _lastDebounceRegion = EdgeRegion.Unknown;
            _lastRegionChangeTime = DateTime.MinValue;
        }

        // ========== 快速切换计数 ==========

        /// <summary>
        /// 切换计数：两次切换间隔 ≤ 1s 视为快速连续切换（累计），超过则重新起算。
        /// 返回是否已累计 ≥3 次（进入图标模态，延迟加载最终面板）。
        /// </summary>
        public bool IsRapidSwitching()
        {
            var now = _clock.Now;
            if ((now - _lastSwitchTime).TotalMilliseconds > RapidSwitchWindowMs)
            {
                _switchCount = 1;
            }
            else
            {
                _switchCount++;
            }
            _lastSwitchTime = now;
            return _switchCount >= 3;
        }

        /// <summary>一轮快速切换结束（稳定/隐藏）：重置切换计数，下次重新分级。</summary>
        public void ResetSwitchCount()
        {
            _switchCount = 0;
            _lastSwitchTime = DateTime.MinValue;
        }

        // ========== 重置 ==========

        /// <summary>清除全部时序状态（ClearEdge 语义）。</summary>
        public void ResetAll()
        {
            _lastDebounceRegion = EdgeRegion.Unknown;
            _lastRegionChangeTime = DateTime.MinValue;
            ResetTriggerDelay();
            ResetSwitchCount();
        }
    }
}