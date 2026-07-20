using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Интервал между выплатами зарплаты (секунды).
    /// </summary>
    public static readonly CVarDef<int> SalaryTime =
        CVarDef.Create("economy.salary_time", 1500, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Включить/отключить выплату зарплат.
    /// </summary>
    public static readonly CVarDef<bool> SalaryEnabled =
        CVarDef.Create("economy.salary_enabled", true, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Глобальный множитель цен в торговых автоматах.
    /// </summary>
    public static readonly CVarDef<float> VendingPriceMultiplier =
        CVarDef.Create("economy.vending_price_multiplier", 1.1f, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Глобальный множитель зарплат.
    /// </summary>
    public static readonly CVarDef<float> SalaryMultiplier =
        CVarDef.Create("economy.salary_multiplier", 1.0f, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// ID прототипа зарплат.
    /// </summary>
    public static readonly CVarDef<string> SalaryPrototypeId =
        CVarDef.Create("economy.salary_prototype_id", "Salaries", CVar.SERVER | CVar.ARCHIVE);
}
