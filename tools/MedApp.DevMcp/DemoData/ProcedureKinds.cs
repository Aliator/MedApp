namespace MedApp.DevMcp.DemoData;

/// <summary>
/// One kind of procedure the demo clinic does, with the details its notes are built from: what it's for, how it's
/// done, the risks talked through at consent, and what it usually finds. All synthetic and generic: no real people,
/// places or brands.
/// </summary>
/// <param name="Title">The procedure's title, as staff give it.</param>
/// <param name="Short">How notes refer to it, e.g. "colonoscopy".</param>
/// <param name="Anaesthetic">How it's done, e.g. "a general anaesthetic", "sedation".</param>
/// <param name="Indications">Why patients are referred for it.</param>
/// <param name="Risks">What's discussed at consent.</param>
/// <param name="Findings">What the procedure record says it found or did.</param>
/// <param name="Results">What results, histology or progress at review say.</param>
/// <param name="Prep">What the patient is asked to do beforehand.</param>
/// <param name="Side">Whether it has a side (left or right), which notes then name.</param>
public sealed record ProcedureKind(
    string Title,
    string Short,
    string Anaesthetic,
    string[] Indications,
    string[] Risks,
    string[] Findings,
    string[] Results,
    string[] Prep,
    bool Side = false);

/// <summary>The kinds of procedure each pathway does.</summary>
public static class ProcedureKinds
{
    private static readonly string[] SurgicalPrep =
    [
        "nil by mouth from midnight, clear fluids until 06:30",
        "shower with the antiseptic wash on the morning of surgery",
        "bring a list of current medicines and any inhalers",
    ];

    public static readonly ProcedureKind Cholecystectomy = new(
        "Laparoscopic cholecystectomy", "lap chole", "a general anaesthetic",
        ["recurrent biliary colic", "gallstones with an episode of cholecystitis", "gallstone pancreatitis, now settled"],
        ["bleeding", "infection", "bile leak", "injury to the bile duct", "conversion to open surgery", "shoulder-tip pain"],
        ["Four-port laparoscopic cholecystectomy. Critical view of safety obtained before clipping.", "Inflamed gallbladder with adhesions to the omentum; dissected free. Cystic duct and artery clipped and divided.", "Gallbladder removed in a retrieval bag; small bile spill washed out."],
        ["Histology: chronic cholecystitis with gallstones; no dysplasia.", "Histology confirms chronic cholecystitis. Nothing unexpected."],
        SurgicalPrep);

    public static readonly ProcedureKind HerniaRepair = new(
        "Inguinal hernia repair", "hernia repair", "a general anaesthetic",
        ["a symptomatic inguinal hernia", "an enlarging inguinal hernia, aching at work", "a reducible inguinal hernia"],
        ["bleeding and bruising", "infection", "recurrence", "chronic groin pain", "numbness near the scar"],
        ["Open mesh repair. Indirect sac reduced; lightweight mesh fixed with absorbable sutures.", "Laparoscopic repair (TEP). Direct defect; mesh placed without fixation.", "Open repair; small indirect sac, mesh repair without tension."],
        ["Wound healed; no recurrence on examination.", "No lump on coughing. Mild numbness below the scar, improving."],
        SurgicalPrep, Side: true);

    public static readonly ProcedureKind KneeArthroscopy = new(
        "Knee arthroscopy", "knee arthroscopy", "a general anaesthetic",
        ["locking and pain from a suspected meniscal tear", "mechanical symptoms after a twisting injury", "a loose body seen on imaging"],
        ["infection", "bleeding into the joint", "blood clots", "stiffness", "no improvement in symptoms"],
        ["Arthroscopy: bucket-handle tear of the medial meniscus, partially resected. Articular cartilage intact.", "Loose body removed from the lateral gutter. Mild chondral wear on the patella.", "Degenerate medial meniscal tear trimmed back to a stable rim."],
        ["Swelling settled; full extension, flexion to 130 degrees.", "Locking has stopped. Mild ache on stairs only."],
        SurgicalPrep, Side: true);

    public static readonly ProcedureKind CarpalTunnel = new(
        "Carpal tunnel release", "carpal tunnel release", "a local anaesthetic",
        ["night-time numbness in the thumb and fingers", "carpal tunnel syndrome confirmed on nerve conduction studies", "weakness of grip and dropping things"],
        ["infection", "tender scar", "pillar pain", "incomplete relief", "nerve injury (rare)"],
        ["Open release under local anaesthetic with a tourniquet. Transverse carpal ligament fully divided; nerve looked healthy.", "Open release; nerve flattened at the distal edge of the ligament. Skin closed with nylon sutures."],
        ["Night symptoms gone. Scar a little tender.", "Numbness much better; grip improving with exercises."],
        ["no fasting needed under local anaesthetic", "wear loose sleeves on the day"], Side: true);

    public static readonly ProcedureKind Varicose = new(
        "Varicose vein ablation", "vein ablation", "a local anaesthetic",
        ["aching and swelling from varicose veins", "varicose veins with skin changes at the ankle", "recurrent bleeding from a varicose vein"],
        ["bruising", "tenderness along the vein", "skin numbness", "blood clots (rare)", "recurrence"],
        ["Endovenous ablation of the great saphenous vein under tumescent local anaesthetic. Good closure on ultrasound.", "Ablation of the small saphenous vein; phlebectomies to two side branches."],
        ["Ultrasound: treated vein closed. Bruising settled.", "Aching much better; stockings for another two weeks."],
        ["bring the compression stocking from pre-assessment", "eat a light breakfast as normal"], Side: true);

    public static readonly ProcedureKind KneeReplacement = new(
        "Total knee replacement", "knee replacement", "a spinal anaesthetic with sedation",
        ["end-stage osteoarthritis of the knee", "knee pain at night despite injections and physiotherapy", "osteoarthritis limiting walking to 200 metres"],
        ["infection", "blood clots", "stiffness", "ongoing pain", "nerve or vessel injury", "loosening needing revision in future"],
        ["Cemented total knee replacement via a medial parapatellar approach. Tourniquet time 78 minutes. Estimated blood loss 200 ml.", "Cemented total knee replacement; balanced in flexion and extension. Tranexamic acid given."],
        ["X-ray: components well aligned. Flexion to 105 degrees, full extension.", "Walking with one stick. Wound healed. Flexion 110 degrees."],
        ["stop ibuprofen a week before", "practise with the crutches from the pre-assessment clinic"], Side: true);

    public static readonly ProcedureKind HipReplacement = new(
        "Total hip replacement", "hip replacement", "a spinal anaesthetic with sedation",
        ["osteoarthritis of the hip with groin pain at night", "hip pain limiting walking and sleep", "avascular necrosis of the femoral head"],
        ["infection", "dislocation", "leg length difference", "blood clots", "nerve injury", "loosening in future"],
        ["Uncemented total hip replacement via a posterior approach. Stable through full range. Estimated blood loss 300 ml.", "Hybrid total hip replacement; posterior capsule repaired. Leg lengths equal on table."],
        ["X-ray: satisfactory position. Walking without aids indoors.", "Pain much improved; mild limp. Hip precautions reviewed."],
        ["stop ibuprofen a week before", "arrange someone to stay for the first week home"], Side: true);

    public static readonly ProcedureKind ShoulderDecompression = new(
        "Arthroscopic subacromial decompression", "shoulder decompression", "a general anaesthetic with a nerve block",
        ["impingement pain despite physiotherapy and an injection", "painful arc and night pain in the shoulder"],
        ["infection", "stiffness", "ongoing pain", "nerve block wearing off at night"],
        ["Arthroscopic subacromial decompression; bursa excised, acromial spur removed. Rotator cuff intact.", "Decompression and excision of the outer end of the clavicle. Small partial cuff tear debrided."],
        ["Pain at night gone. Elevation to 160 degrees.", "Improving; still weak above shoulder height. Continue physiotherapy."],
        SurgicalPrep, Side: true);

    public static readonly ProcedureKind Colonoscopy = new(
        "Colonoscopy", "colonoscopy", "sedation",
        ["a change in bowel habit", "iron-deficiency anaemia", "a positive faecal immunochemical test", "rectal bleeding", "surveillance after polyps"],
        ["bleeding", "perforation (about 1 in 1,000)", "missed lesions", "reaction to sedation", "an incomplete test"],
        ["Complete to the caecum; terminal ileum intubated. Two diminutive polyps in the sigmoid removed with cold snare and retrieved.", "Complete to the caecum. Mild diverticular disease in the sigmoid colon; nothing else.", "Complete. A 12 mm polyp in the ascending colon removed by EMR; site clipped. Biopsies taken for microscopic colitis.", "Normal colonoscopy to the caecum. Good bowel preparation."],
        ["Histology: tubular adenomas with low-grade dysplasia, completely removed. Surveillance colonoscopy in 3 years.", "Biopsies normal; no microscopic colitis. Symptoms likely functional.", "Histology: hyperplastic polyps only. No further colonoscopy needed.", "Normal test. Back to the GP for ongoing management."],
        ["start the low-residue diet two days before", "take the bowel preparation in two doses, the evening before and the morning of the test", "arrange an escort home after sedation"]);

    public static readonly ProcedureKind Gastroscopy = new(
        "Upper GI endoscopy", "gastroscopy", "throat spray, with sedation if wanted",
        ["indigestion not settling on treatment", "difficulty swallowing", "iron-deficiency anaemia", "weight loss with upper abdominal pain", "surveillance of Barrett's oesophagus"],
        ["sore throat", "bleeding", "perforation (rare)", "reaction to sedation", "aspiration"],
        ["Mild antral gastritis; biopsies taken for Helicobacter testing. Duodenum normal.", "Small hiatus hernia with grade A oesophagitis. Biopsies from the second part of the duodenum.", "Normal oesophagus, stomach and duodenum.", "Short segment of Barrett's oesophagus (C1M2); quadrantic biopsies taken."],
        ["Biopsies: Helicobacter positive. Eradication therapy started via the GP.", "Duodenal biopsies normal; coeliac disease excluded.", "Barrett's without dysplasia. Repeat surveillance in 3 years.", "Gastritis only; continue the acid-suppressing tablet for 8 weeks."],
        ["nothing to eat for six hours before", "sips of water allowed up to two hours before", "stop omeprazole two weeks before if able"]);

    public static readonly ProcedureKind Sigmoidoscopy = new(
        "Flexible sigmoidoscopy", "flexi sig", "no sedation (gas and air offered)",
        ["fresh rectal bleeding", "left-sided abdominal pain with looser stools", "follow-up of left-sided colitis"],
        ["bleeding", "perforation (rare)", "discomfort", "an incomplete test"],
        ["Examined to the splenic flexure. Small internal haemorrhoids; nothing else.", "Mild proctitis; biopsies taken. Rest of the examined colon normal.", "Single 6 mm rectal polyp removed and retrieved."],
        ["Biopsies show mild active proctitis; treatment started.", "Polyp was hyperplastic. No further tests needed.", "Haemorrhoids only; banding offered if symptoms continue."],
        ["use the enema at home two hours before", "eat normally the day before"]);

    public static readonly ProcedureKind Cystoscopy = new(
        "Flexible cystoscopy", "cystoscopy", "a local anaesthetic gel",
        ["visible blood in the urine", "recurrent urine infections", "surveillance after bladder tumour treatment"],
        ["urine infection", "blood in the urine for a day or two", "stinging on passing urine"],
        ["Urethra and bladder normal; no tumour or stones seen.", "Mild trabeculation of the bladder; no tumour. Prostatic urethra a little narrowed.", "Small area of erythema at the dome; biopsy arranged under GA."],
        ["Normal cystoscopy and normal imaging: no further tests needed.", "Trabeculation consistent with outflow obstruction; started on tamsulosin via the GP.", "Biopsy: inflammatory change only."],
        ["drink normally before", "bring a urine sample on the day"]);

    public static readonly ProcedureKind SkinLesion = new(
        "Skin lesion excision", "excision", "a local anaesthetic",
        ["a changing pigmented lesion on the back", "a non-healing lesion on the forearm", "a lesion catching on clothing"],
        ["bleeding", "infection", "scarring", "the need for further surgery if margins aren't clear"],
        ["Elliptical excision with 2 mm margins; closed with interrupted nylon sutures.", "Excision with 3 mm margins; layered closure. Specimen orientated with a suture.", "Shave excision; haemostasis with silver nitrate."],
        ["Histology: benign compound naevus, completely excised.", "Histology: seborrhoeic keratosis. No further action.", "Histology: basal cell carcinoma, completely excised with clear margins. Discharged with skin-check advice."],
        ["take usual medicines as normal", "wear a loose top"]);

    public static readonly ProcedureKind Cyst = new(
        "Sebaceous cyst removal", "cyst removal", "a local anaesthetic",
        ["a recurrently infected cyst on the upper back", "an enlarging cyst on the scalp", "a cyst on the neck catching on a collar"],
        ["bleeding", "infection", "scarring", "recurrence if the lining isn't all removed"],
        ["Cyst excised intact with its punctum; closed with nylon sutures.", "Cyst removed in two pieces after rupture; cavity washed out, loosely closed."],
        ["Healed well. Histology: epidermoid cyst.", "Wound healed with a small scar. No recurrence."],
        ["wash the area the morning before"]);

    public static readonly ProcedureKind Toenail = new(
        "Ingrown toenail removal", "nail surgery", "a ring-block local anaesthetic",
        ["a recurrently infected ingrown toenail", "pain from an ingrown toenail despite conservative care"],
        ["infection", "regrowth", "pain once the block wears off"],
        ["Wedge resection of the lateral nail edge with phenolisation of the nail bed.", "Partial nail avulsion of both edges; phenol applied for 3 minutes."],
        ["Healing well; dressing reduced. No regrowth.", "Small area of granulation tissue, treated with silver nitrate."],
        ["wear open-toed shoes on the day", "bring someone to drive home"], Side: true);

    public static readonly ProcedureKind JointInjection = new(
        "Joint injection", "joint injection", "a local anaesthetic",
        ["painful osteoarthritis of the knee", "frozen shoulder in the painful phase", "trigger thumb"],
        ["infection (rare)", "a flare of pain for a day or two", "skin thinning or pale skin at the site", "temporary rise in blood sugar"],
        ["Injected with 40 mg methylprednisolone and 3 ml 1% lidocaine under aseptic technique.", "Ultrasound-guided injection; 40 mg triamcinolone with lidocaine."],
        ["Good relief so far; pain score down from 7 to 3.", "Partial relief only; to discuss other options."],
        ["check blood sugar more often for two days if diabetic"], Side: true);

    public static readonly ProcedureKind ShoulderRehab = new(
        "Shoulder rehabilitation", "shoulder", "physiotherapy",
        ["shoulder pain and stiffness after a fall", "rotator cuff tendinopathy", "rehabilitation after shoulder decompression"],
        [], [],
        ["Goals met: reaching the top shelf without pain.", "Elevation to 165 degrees; strength 5/5 except external rotation 4+/5."],
        ["bring a loose vest top"], Side: true);

    public static readonly ProcedureKind KneeRehab = new(
        "Knee rehabilitation", "knee", "physiotherapy",
        ["rehabilitation after knee replacement", "patellofemoral pain on stairs", "rehabilitation after knee arthroscopy"],
        [], [],
        ["Goals met: walking 1 km without a stick, stairs reciprocally.", "Flexion 115 degrees, full extension. Back to gardening."],
        ["bring shorts"], Side: true);

    public static readonly ProcedureKind BackPain = new(
        "Lower back pain programme", "lower back", "physiotherapy",
        ["non-specific lower back pain for 3 months", "back pain with sciatica, improving", "recurrent back pain from lifting at work"],
        [], [],
        ["Goals met: back at work on full duties; pain 2/10.", "Walking 30 minutes daily without leg symptoms."],
        ["wear comfortable clothing"]);

    public static readonly ProcedureKind AnkleRehab = new(
        "Ankle rehabilitation", "ankle", "physiotherapy",
        ["a lateral ankle sprain playing football", "ankle instability after a sprain", "Achilles tendinopathy"],
        [], [],
        ["Goals met: single-leg balance 30 seconds, jogging without pain.", "Back to running 5 km; hopping pain-free."],
        ["bring trainers"], Side: true);

    /// <summary>The kinds of procedure each pathway does.</summary>
    public static IReadOnlyList<ProcedureKind> For(Pathway pathway) => pathway switch
    {
        Pathway.DayCase or Pathway.DayCase2024 => [Cholecystectomy, HerniaRepair, KneeArthroscopy, CarpalTunnel, Varicose],
        Pathway.Surgical => [KneeReplacement, HipReplacement, Cholecystectomy, ShoulderDecompression],
        Pathway.Diagnostic => [Colonoscopy, Colonoscopy, Gastroscopy, Gastroscopy, Sigmoidoscopy, Cystoscopy],
        Pathway.Minor => [SkinLesion, SkinLesion, Cyst, Toenail, JointInjection],
        Pathway.Physio => [ShoulderRehab, KneeRehab, BackPain, AnkleRehab],
        _ => [],
    };

    /// <summary>Whether a pathway sees children (most of the clinic's work is adults only).</summary>
    public static bool SeesChildren(Pathway pathway) => pathway is Pathway.Minor or Pathway.Physio;
}
