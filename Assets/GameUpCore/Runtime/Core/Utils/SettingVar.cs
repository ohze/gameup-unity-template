namespace GameUp.Core
{
    public abstract class SettingVar<T> where T : struct
    {
        public readonly Signal<T> OnValueChange = new();
        
        protected readonly string Key;
        protected T DefaultValue;
        protected T? V;

        protected SettingVar(string key, T defaultV = default)
        {
            this.Key = key;
            DefaultValue = defaultV;
        }

        public abstract T Value { get; set; }

        public void UpdateWithoutDispatch(T newValue)
        {
            V = newValue;
        }

        public abstract void AddValueWithoutDispatch(T newValue);

        public void Dispatch()
        {
            OnValueChange.Dispatch(Value);
        }
    }

    public class BooleanVar : SettingVar<bool>
    {
        public BooleanVar(string key, bool defaultV = true) : base(key, defaultV)
        {
        }

        public override bool Value
        {
            get
            {
                V ??= LocalStorageUtils.GetBoolean(Key, DefaultValue);
                return V.Value;
            }
            set
            {
                V = value;
                LocalStorageUtils.SetBoolean(Key, value);
                OnValueChange.Dispatch(value);
            }
        }

        public override void AddValueWithoutDispatch(bool newValue)
        {
            V = newValue;
        }
    }

    public class IntVar : SettingVar<int>
    {
        public IntVar(string key, int defaultV = 0) : base(key, defaultV)
        {
        }

        public override int Value
        {
            get
            {
                V ??= LocalStorageUtils.GetInt(Key, DefaultValue);

                return V.Value;
            }
            set
            {
                V = value;
                LocalStorageUtils.SetInt(Key, value);
                OnValueChange.Dispatch(value);
            }
        }

        public override void AddValueWithoutDispatch(int newValue)
        {
            V += newValue;
        }
    }

    public class FloatVar : SettingVar<float>
    {
        public FloatVar(string key, float defaultV = 0f) : base(key, defaultV)
        {
        }

        public override float Value
        {
            get
            {
                V ??= LocalStorageUtils.GetFloat(Key, DefaultValue);
                return V.Value;
            }
            set
            {
                V = value;
                LocalStorageUtils.SetFloat(Key, value);
                OnValueChange.Dispatch(value);
            }
        }

        public override void AddValueWithoutDispatch(float newValue)
        {
            V = newValue;
        }
    }
    
    public class LongVar : SettingVar<long>
    {
        public LongVar(string key, long defaultV = 0) : base(key, defaultV)
        {
        }

        public override long Value
        {
            get
            {
                V ??= LocalStorageUtils.GetLong(Key, DefaultValue);

                return V.Value;
            }
            set
            {
                V = value;
                LocalStorageUtils.SetLong(Key, value);
                OnValueChange.Dispatch(value);
            }
        }

        public override void AddValueWithoutDispatch(long newValue)
        {
            V += newValue;
        }
    }
}