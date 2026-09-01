using ShoreHue.Core.Infrastructure.Logging;
using ShoreHue.Core.Infrastructure.Service;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.src.core.Services.System;
using System;

namespace ShoreHue.Core.Services
{
    public class ModeManager : IModeService, IService
    {
        private readonly ISettingsService _settings;
        private bool _isDoNotDisturb = false;

        public event Action<bool>? ModeChanged;

        public string Name => "ModeManager";
        public bool IsInitialized { get; private set; } = false;

        public ModeManager(ISettingsService settings)
        {
            _settings = settings;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            if (_settings.RememberDndMode)
            {
                _isDoNotDisturb = _settings.DndModeEnabled;
            }
            else
            {
                _isDoNotDisturb = false;
            }
            IsInitialized = true;
            ModeChanged?.Invoke(_isDoNotDisturb);
            LogManager.Debug($"ModeManager 初始化完成，勿扰模式: {_isDoNotDisturb}");
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            if (_settings.RememberDndMode)
            {
                _settings.DndModeEnabled = _isDoNotDisturb;
            }
            IsInitialized = false;
            LogManager.Debug("ModeManager 已关闭");
        }

        public bool IsDoNotDisturb
        {
            get => _isDoNotDisturb;
            set
            {
                if (_isDoNotDisturb == value) return;
                _isDoNotDisturb = value;
                ModeChanged?.Invoke(value);

                if (_settings.RememberDndMode)
                {
                    _settings.DndModeEnabled = value;
                }
            }
        }

        public void Toggle() => IsDoNotDisturb = !IsDoNotDisturb;
    }
}