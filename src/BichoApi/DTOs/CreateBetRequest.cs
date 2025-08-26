public enum Modality {
    CENTENA, CENTENA_INV, CENTENA_3X, CENTENA_ESQ, CENTENA_INV_ESQ,
    MILHAR, MILHAR_INV, MILHAR_E_CT,
    UNIDADE, DEZENA, DEZENA_ESQ, DEZENA_MEIO,
    DUQUE_DE_DEZENA, DUQUE_DEZENA_ESQ, DUQUE_DEZENA_MEIO,
    TERNO_DZ_SECO, TERNO_DZ_SECO_ESQ, TERNO_DZ,
    GRUPO, GRUPO_ESQ, GRUPO_MEIO,
    DUQUE_DE_GRUPO, DUQUE_DE_GRUPO_ESQ, DUQUE_DE_GRUPO_MEIO,
    TERNO_GP, TERNO_GP_ESQ, TERNO_GP_MEIO,
    QUADRA_GP, QUADRA_GP_ESQ, QUADRA_GP_MEIO,
    QUINA_GP, QUINA_GP_ESQ, QUINA_GP_MEIO,
    SENA_GP, SENA_GP_ESQ, SENA_GP_MEIO,
    PALPITAO, SENINHA, QUININHA, LOTINHA,
    PASSE_VAI, PASSE_VAI_E_VEM
}

public enum PositionPick { RIGHT, LEFT, MIDDLE }
public enum PrizeWindow { _1_ONLY, _1_3, _1_5, _1_6 }

public sealed class CreateBetRequest {
    public long UserId { get; set; }
    public Modality Modality { get; set; }
    public List<PositionPick> Positions { get; set; } = new();
    public PrizeWindow PrizeWindow { get; set; }
    public long StakeCents { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}