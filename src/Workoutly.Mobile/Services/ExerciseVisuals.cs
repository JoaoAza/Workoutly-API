namespace Workoutly.Mobile.Services;

public sealed record ExerciseVisual(string ImageSource, string? QuickInstruction);

/// <summary>
/// Ilustrações do catálogo por ID e nome exato. Cadastros desconhecidos recebem apenas um ícone genérico.
/// </summary>
public static class ExerciseVisuals
{
    private const string SquatId = "e1000000-0000-0000-0000-000000000001";
    private static readonly IReadOnlyDictionary<string, ExerciseVisual> Catalogue =
        new Dictionary<string, ExerciseVisual>(StringComparer.Ordinal)
        {
            [SquatId] = new("barbell_squat.png", "Apoie a barra com estabilidade, flexione quadris e joelhos com os pés firmes e retorne de forma controlada."),
            ["e1000000-0000-0000-0000-000000000002"] = new("leg_press.png", "Mantenha a lombar apoiada, desça a plataforma com controle e empurre sem travar o movimento."),
            ["e1000000-0000-0000-0000-000000000003"] = new("leg_extension.png", "Ajuste o equipamento, estenda os joelhos com controle e retorne devagar à posição inicial."),
            ["e1000000-0000-0000-0000-000000000004"] = new("bench_press.png", "Com costas e pés apoiados, desça a barra com controle em direção ao peito e empurre mantendo a estabilidade."),
            ["e1000000-0000-0000-0000-000000000005"] = new("bent_over_row.png", "Incline o tronco com a coluna neutra, puxe a barra em direção ao abdômen e controle a descida."),
            ["e1000000-0000-0000-0000-000000000006"] = new("lat_pulldown.png", "Sente-se firme, traga a barra em direção à parte alta do peito e retorne sem balançar o tronco."),
            ["e1000000-0000-0000-0000-000000000007"] = new("dumbbell_shoulder_press.png", "Comece com os halteres na altura dos ombros e eleve-os de forma controlada, mantendo o tronco estável."),
            ["e1000000-0000-0000-0000-000000000008"] = new("barbell_curl.png", "Mantenha os cotovelos próximos ao corpo, flexione os braços e abaixe a barra de forma controlada."),
            ["e1000000-0000-0000-0000-000000000009"] = new("triceps_pushdown.png", "Mantenha os cotovelos junto ao corpo, estenda os braços para baixo e retorne devagar."),
            ["e1000000-0000-0000-0000-000000000010"] = new("calf_raise.png", "Apoie-se com estabilidade, eleve os calcanhares e desça de forma controlada."),
            ["e1000000-0000-0000-0000-000000000011"] = new("dumbbell_fly.png", "Com as costas apoiadas no banco, abra os braços com leve flexão nos cotovelos e retorne com controle."),
            ["e1000000-0000-0000-0000-000000000012"] = new("lateral_raise.png", "Eleve os braços lateralmente com controle, mantendo o tronco estável."),
            ["e1000000-0000-0000-0000-000000000013"] = new("incline_bench_press.png", "Apoie as costas no banco inclinado e mova a barra com controle."),
            ["e1000000-0000-0000-0000-000000000014"] = new("dumbbell_bench_press.png", "Com as costas apoiadas, empurre os halteres e retorne de forma controlada."),
            ["e1000000-0000-0000-0000-000000000015"] = new("push_up.png", "Mantenha o corpo alinhado enquanto flexiona e estende os braços."),
            ["e1000000-0000-0000-0000-000000000016"] = new("seated_row.png", "Sente-se com o tronco firme e puxe o cabo em direção ao abdômen."),
            ["e1000000-0000-0000-0000-000000000017"] = new("one_arm_dumbbell_row.png", "Apoie o corpo no banco e puxe o halter mantendo a coluna estável."),
            ["e1000000-0000-0000-0000-000000000018"] = new("pull_up.png", "Puxe o corpo com controle e evite balançar o tronco."),
            ["e1000000-0000-0000-0000-000000000019"] = new("deadlift.png", "Mantenha a barra próxima ao corpo e coordene a extensão de quadris e joelhos."),
            ["e1000000-0000-0000-0000-000000000020"] = new("romanian_deadlift.png", "Leve o quadril para trás com leve flexão dos joelhos e mantenha a coluna estável."),
            ["e1000000-0000-0000-0000-000000000021"] = new("lying_leg_curl.png", "Ajuste o apoio da máquina e flexione os joelhos sem perder a posição do quadril."),
            ["e1000000-0000-0000-0000-000000000022"] = new("seated_leg_curl.png", "Mantenha as costas apoiadas e flexione os joelhos de forma controlada."),
            ["e1000000-0000-0000-0000-000000000023"] = new("walking_lunge.png", "Dê um passo à frente, flexione os joelhos com estabilidade e avance alternando as pernas."),
            ["e1000000-0000-0000-0000-000000000024"] = new("bulgarian_split_squat.png", "Apoie o pé de trás no banco e desça com controle mantendo o equilíbrio."),
            ["e1000000-0000-0000-0000-000000000025"] = new("hip_thrust.png", "Apoie a parte alta das costas no banco e eleve o quadril com controle."),
            ["e1000000-0000-0000-0000-000000000026"] = new("seated_calf_raise.png", "Com os joelhos apoiados, eleve e abaixe os calcanhares de forma controlada."),
            ["e1000000-0000-0000-0000-000000000027"] = new("hammer_curl.png", "Flexione os cotovelos mantendo as palmas voltadas uma para a outra."),
            ["e1000000-0000-0000-0000-000000000028"] = new("preacher_curl.png", "Mantenha os braços apoiados no banco e flexione os cotovelos com controle."),
            ["e1000000-0000-0000-0000-000000000029"] = new("skull_crusher.png", "Com as costas apoiadas, flexione e estenda os cotovelos mantendo os braços estáveis."),
            ["e1000000-0000-0000-0000-000000000030"] = new("front_raise.png", "Eleve os braços à frente com controle e sem balançar o tronco."),
        };

    private static readonly IReadOnlyDictionary<string, ExerciseVisual> NamedCatalogue =
        new Dictionary<string, ExerciseVisual>(StringComparer.OrdinalIgnoreCase)
        {
            ["Crucifixo com halteres"] = Catalogue["e1000000-0000-0000-0000-000000000011"],
            ["Elevação lateral"] = Catalogue["e1000000-0000-0000-0000-000000000012"],
            ["Supino inclinado"] = Catalogue["e1000000-0000-0000-0000-000000000013"],
            ["Supino com halteres"] = Catalogue["e1000000-0000-0000-0000-000000000014"],
            ["Flexão de braços"] = Catalogue["e1000000-0000-0000-0000-000000000015"],
            ["Remada baixa"] = Catalogue["e1000000-0000-0000-0000-000000000016"],
            ["Remada unilateral"] = Catalogue["e1000000-0000-0000-0000-000000000017"],
            ["Barra fixa"] = Catalogue["e1000000-0000-0000-0000-000000000018"],
            ["Levantamento terra"] = Catalogue["e1000000-0000-0000-0000-000000000019"],
            ["Levantamento terra romeno"] = Catalogue["e1000000-0000-0000-0000-000000000020"],
            ["Mesa flexora"] = Catalogue["e1000000-0000-0000-0000-000000000021"],
            ["Cadeira flexora"] = Catalogue["e1000000-0000-0000-0000-000000000022"],
            ["Afundo com deslocamento"] = Catalogue["e1000000-0000-0000-0000-000000000023"],
            ["Agachamento búlgaro"] = Catalogue["e1000000-0000-0000-0000-000000000024"],
            ["Elevação pélvica"] = Catalogue["e1000000-0000-0000-0000-000000000025"],
            ["Panturrilha sentado"] = Catalogue["e1000000-0000-0000-0000-000000000026"],
            ["Rosca martelo"] = Catalogue["e1000000-0000-0000-0000-000000000027"],
            ["Rosca Scott"] = Catalogue["e1000000-0000-0000-0000-000000000028"],
            ["Tríceps testa"] = Catalogue["e1000000-0000-0000-0000-000000000029"],
            ["Elevação frontal"] = Catalogue["e1000000-0000-0000-0000-000000000030"],
        };

    private static readonly ExerciseVisual Generic = new("exercise_generic.png", null);

    public static ExerciseVisual Find(string exerciseId, string name) =>
        Catalogue.TryGetValue(exerciseId, out var visual) ? visual :
        NamedCatalogue.TryGetValue(name.Trim(), out visual) ? visual : Generic;
}
