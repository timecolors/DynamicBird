using System;

namespace ShoreHue.Core.Services
{
    /// <summary>
    /// 管理面板锁定状态（拖拽/调整大小时锁定，防止自动隐藏）
    /// </summary>
    public class PanelLockManager
    {
        private bool _isLocked = false;

        public bool IsLocked => _isLocked;

        public event Action<bool>? LockChanged;

        public void SetLock(bool locked)
        {
            if (_isLocked == locked) return;
            _isLocked = locked;
            LockChanged?.Invoke(locked);
        }

        public void Lock() => SetLock(true);
        public void Unlock() => SetLock(false);
    }
}