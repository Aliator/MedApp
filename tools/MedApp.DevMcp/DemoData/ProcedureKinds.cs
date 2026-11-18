namespace MedApp.DevMcp.DemoData;

/// <summary>
/// How one referral turns out, so its notes agree from stage to stage: what the clinician found at the visit, what the
/// lab reported, the plan that followed, and (on the skin lesion pathway) what was done at the treatment visit.
/// </summary>
/// <param name="Seen">What the clinic visit found: the lesion described, the examination, the measurements.</param>
/// <param name="Result">The lab's report in words (histology, or blood and urine results); for a course, the result at its end. Empty when the pathway has none.</param>
/// <param name="Plan">The plan agreed, as the notes and the letter to the GP give it.</param>
/// <param name="Treatment">What was done at the treatment visit, on a pathway with one.</param>
public sealed record KindOutcome(string Seen, string Result, string Plan, string Treatment = "");

/// <summary>
/// One kind of referral the demo centre takes, with the details its notes are built from: why patients are referred,
/// what's taken or tested, what's found and what follows. All synthetic and generic: no real people, places or brands
/// (only generic medicines). Nothing here decides anything clinical: it's the text staff "wrote" in the story.
/// </summary>
/// <param name="Title">The record's title, as staff give it.</param>
/// <param name="Short">How notes refer to it, e.g. "the lesion".</param>
/// <param name="Sample">What's taken or tested, e.g. "4 mm punch biopsy", "PSA and kidney function".</param>
/// <param name="Team">Where results are reviewed, e.g. "the skin cancer team meeting".</param>
/// <param name="NextStep">What the first clinic visit arranges while results are still to come; null when the visit itself agrees the plan.</param>
/// <param name="Indications">Why patients are referred.</param>
/// <param name="Risks">What's discussed before a biopsy or sample.</param>
/// <param name="SampleNotes">How taking the biopsy or sample went.</param>
/// <param name="Outcomes">How it turns out (one is chosen for each referral).</param>
/// <param name="FollowUps">What follow-up finds.</param>
/// <param name="Medicines">What a medicines review changes.</param>
/// <param name="Sites">Where on the body, when it has a site; one is chosen for each referral, and its notes name it.</param>
public sealed record ProcedureKind(
    string Title,
    string Short,
    string Sample,
    string Team,
    string? NextStep,
    string[] Indications,
    string[] Risks,
    string[] SampleNotes,
    KindOutcome[] Outcomes,
    string[] FollowUps,
    string[] Medicines,
    string[] Sites);

/// <summary>The kinds of referral each pathway takes.</summary>
public static class ProcedureKinds
{
    private const string SkinTeam = "the skin cancer team meeting";

    private static readonly string[] SkinSites =
    [
        "left forearm", "right forearm", "left shin", "right calf", "upper back", "left shoulder", "right cheek", "scalp",
        "left temple", "chest",
    ];

    private static readonly string[] SkinRisks =
    [
        "bleeding", "infection", "a scar", "numbness near the scar", "the need for more surgery if the report advises it",
    ];

    private static readonly string[] SkinSampleNotes =
    [
        "Local anaesthetic: lidocaine 1% with adrenaline, 2 ml. One suture, out in 7 days.",
        "Local anaesthetic: lidocaine 1% with adrenaline, 3 ml. Closed with two sutures; dressing applied.",
        "Local anaesthetic: lidocaine 1% with adrenaline, 2 ml. Haemostasis with pressure; simple dressing.",
    ];

    private static readonly string[] SkinFollowUps =
    [
        "Wound healed; scar flat and settling.",
        "Scar a little raised and itchy: silicone gel suggested.",
        "Healed well. Skin self-check advice gone through again.",
        "Stitch marks fading; no new lesions noticed.",
    ];

    public static readonly ProcedureKind PigmentedLesion = new(
        "Changing pigmented lesion", "the lesion", "excision biopsy with a 2 mm margin", SkinTeam,
        "Excision biopsy on the next minor surgery list",
        ["a mole that has changed shape over three months", "a pigmented lesion that has darkened and itches", "a new dark mole noticed by a relative"],
        SkinRisks, SkinSampleNotes,
        [
            new("6 mm pigmented lesion with an irregular border and two colours. Dermoscopy: atypical network. Full skin check: nothing else of concern.",
                "Histology: melanoma in situ, excised with a narrow margin.",
                "Wider excision with a 5 mm margin agreed at the team meeting; nurse specialist's contact details given.",
                "Wider excision of the scar with a 5 mm margin; closed in layers. Specimen sent."),
            new("8 mm pigmented lesion, asymmetrical, with a darker area at one edge. Dermoscopy: irregular globules.",
                "Histology: dysplastic naevus with severe atypia; margin under 1 mm.",
                "Re-excision of the scar recommended, to clear the margin.",
                "Re-excision of the scar with a 2 mm margin. Specimen sent."),
            new("5 mm evenly brown, raised mole; a second, similar one nearby catches on clothing. Dermoscopy: regular pattern in both.",
                "Histology: benign compound naevus, completely excised.",
                "Benign; the second mole to be removed at the patient's request, as it catches and bleeds.",
                "Shave excision of the second mole; haemostasis with aluminium chloride. Specimen sent."),
        ],
        SkinFollowUps, [], SkinSites);

    public static readonly ProcedureKind NonHealingLesion = new(
        "Non-healing skin lesion", "the lesion", "4 mm punch biopsy", SkinTeam,
        "Punch biopsy on the next minor surgery list",
        ["a sore that has not healed in eight weeks", "a pearly lump that bleeds when washed", "a crusted patch that keeps coming back"],
        SkinRisks, SkinSampleNotes,
        [
            new("8 mm pearly nodule with a rolled edge and fine surface vessels.",
                "Histology: nodular basal cell carcinoma.",
                "Excision with a 4 mm margin under local anaesthetic.",
                "Excised with a 4 mm margin; closed directly with interrupted sutures. Specimen marked with a suture and sent."),
            new("12 mm scaly, tender plaque on sun-damaged skin.",
                "Histology: squamous cell carcinoma in situ.",
                "Curettage and cautery agreed; a treatment cream discussed as the alternative.",
                "Curettage and cautery, two cycles. Curettings sent."),
            new("5 mm ulcerated papule with a raised edge.",
                "Histology: superficial basal cell carcinoma.",
                "Excision, which the patient preferred to a cream.",
                "Excised with a 3 mm margin; layered closure. Specimen sent."),
        ],
        SkinFollowUps, [], SkinSites);

    public static readonly ProcedureKind ScalyPatch = new(
        "Scaly patch on sun-damaged skin", "the patch", "shave biopsy", SkinTeam,
        "Shave biopsy of the thickest patch on the next minor surgery list",
        ["rough, scaly patches that catch on a comb", "a thickened scaly patch that has become sore", "several rough patches after years of outdoor work"],
        SkinRisks, SkinSampleNotes,
        [
            new("Several rough, red, scaly patches; the largest is 10 mm and thickened.",
                "Histology: actinic keratosis; no invasive change.",
                "Cryotherapy to the thick patch; a field treatment cream for the rest, via the GP.",
                "Cryotherapy, two freeze-thaw cycles of 10 seconds, to the thick patch and three smaller ones."),
            new("15 mm thick, scaly plaque with a tender base.",
                "Histology: hypertrophic actinic keratosis with moderate dysplasia.",
                "Curettage and cautery to the plaque.",
                "Curettage and cautery. Curettings sent."),
        ],
        SkinFollowUps, [], SkinSites);

    public static readonly ProcedureKind Eczema = new(
        "Eczema review", "eczema", "", "the skin team", null,
        ["eczema not controlled with the creams from the GP", "hand eczema affecting work", "widespread eczema with broken sleep"],
        [], [],
        [
            new("Dry, scratched eczema in the elbow and knee creases and on the hands; no infection. About a tenth of the skin affected.",
                "",
                "Emollient at least four times a day; a potent steroid ointment for two weeks, then at weekends only"),
            new("Thickened, cracked eczema on both palms; patch testing discussed.",
                "",
                "Greasy emollient under cotton gloves at night; a potent steroid ointment for four weeks; glove advice for work"),
        ],
        ["Skin much calmer; sleeping through the night.", "A flare last week after stopping the ointment: restarted, settling.", "Hands healed; back to full duties at work."],
        ["Emollient changed to an ointment; 500 g a week on repeat.", "Steroid stepped down to a moderate-strength ointment; fingertip units explained again.", "Soap substitute added; the antiseptic wash stopped, as it stung."],
        []);

    public static readonly ProcedureKind Psoriasis = new(
        "Psoriasis review", "psoriasis", "", "the skin team", null,
        ["plaque psoriasis spreading despite treatment", "scalp psoriasis with thick scale", "psoriasis with sore finger joints"],
        [], [],
        [
            new("Chronic plaque psoriasis on the elbows, knees and lower back; nails pitted. No swollen joints today.",
                "",
                "A vitamin D and steroid foam once a day for four weeks; scalp treatment left on overnight"),
            new("Thick plaques on the shins and scalp; severity score 9.",
                "",
                "Creams and scalp treatment optimised; light treatment to be discussed if no better at follow-up"),
        ],
        ["Plaques thinner and paler; scalp clear.", "Elbows better, shins unchanged: to keep going for another month.", "Itch settled; using the foam twice a week."],
        ["The foam explained: once a day for four weeks, then twice a week.", "Coal tar shampoo added for the scalp.", "Other tablets checked: none known to make psoriasis worse."],
        []);

    public static readonly ProcedureKind Acne = new(
        "Acne review", "acne", "", "the skin team", null,
        ["acne leaving scars despite antibiotics from the GP", "persistent acne on the face and back"],
        [], [],
        [
            new("Moderate inflamed acne on the face and upper back, with early scarring.",
                "",
                "A retinoid gel with benzoyl peroxide at night; an oral antibiotic for three months, then review"),
        ],
        ["Fewer new spots; skin less sore.", "Dryness from the gel: using it on alternate nights with a moisturiser.", "Much improved; scars fading."],
        ["Oral antibiotic to stop at three months; the gel to continue.", "Retinoid gel: start on alternate nights, with a moisturiser.", "Contraception checked before any change of treatment."],
        []);

    private static readonly string[] GynaeRisks = ["cramping at the time", "spotting for a day or two", "a small chance the sample is too small to report"];

    private static readonly string[] GynaeSampleNotes =
    [
        "Taken in clinic with a speculum; tolerated well, with mild cramping only.",
        "Two passes needed for an adequate sample; cramping settled within minutes.",
        "Taken in clinic; the nurse stayed with the patient throughout.",
    ];

    public static readonly ProcedureKind PostMenopausalBleed = new(
        "Post-menopausal bleeding", "the bleeding", "endometrial sample", "the gynaecology team review",
        "Endometrial sample to be taken at the next visit",
        ["one episode of bleeding, three years after the menopause", "spotting on and off for two months", "bleeding after ten years without periods"],
        GynaeRisks, GynaeSampleNotes,
        [
            new("Abdomen soft; cervix looks healthy. Scan in clinic: womb lining 3 mm, regular.",
                "Histology: inactive endometrium; no hyperplasia or malignancy.",
                "Reassured; vaginal oestrogen suggested via the GP for dryness; to come back if it bleeds again"),
            new("Cervix healthy. Scan in clinic: womb lining 7 mm, with a small polyp suspected.",
                "Histology: fragments of a benign endometrial polyp; no atypia.",
                "Referred to the hospital for a camera test and removal of the polyp"),
            new("Thin, dry tissues on examination. Scan in clinic: womb lining 5 mm.",
                "Histology: atrophic endometrium; a scanty sample, with no atypia seen.",
                "Vaginal oestrogen started via the GP; a repeat scan only if the bleeding returns"),
            new("Scan in clinic: womb lining 11 mm, irregular.",
                "Histology: endometrial hyperplasia with atypia.",
                "Referred urgently to the hospital gynaecology team; told in clinic, with the nurse present"),
        ],
        [], [], []);

    public static readonly ProcedureKind BleedingOnHrt = new(
        "Bleeding on HRT", "the bleeding", "endometrial sample", "the gynaecology team review",
        "Endometrial sample to be taken at the next visit",
        ["unscheduled bleeding six months after starting HRT", "bleeding after a change of HRT patch"],
        GynaeRisks, GynaeSampleNotes,
        [
            new("Cervix healthy. Scan in clinic: womb lining 4 mm.",
                "Histology: inactive endometrium on HRT; no hyperplasia.",
                "HRT changed to a continuous combined type, via the GP; review if the bleeding goes on past three months"),
            new("Scan in clinic: womb lining 6 mm, regular.",
                "Histology: weakly proliferative endometrium; no hyperplasia.",
                "Progestogen dose increased via the GP; to come back if it has not settled in three months"),
        ],
        [], [], []);

    private static readonly string[] UrologyFollowUps =
    [
        "Getting up once a night now; happy to carry on with the tablet.",
        "Symptoms unchanged: dose reviewed with the GP.",
        "Repeat PSA stable.",
        "No new symptoms; knows when to ask the GP to re-refer.",
    ];

    public static readonly ProcedureKind RaisedPsa = new(
        "Raised PSA", "the raised PSA", "PSA and kidney function", "the urology results meeting", null,
        ["a PSA above the range for his age on a routine blood test", "a PSA rising over two tests at the GP", "a raised PSA with a family history of prostate cancer"],
        [], [],
        [
            new("Examination: smooth, moderately enlarged prostate; no nodules.",
                "PSA 5.8 micrograms/L on repeat; kidney function normal; urine clear.",
                "Benign feel and a stable PSA: PSA again in six months via the GP, with advice on when to re-refer"),
            new("Examination: a firm area on the left side of the prostate.",
                "PSA 9.4 micrograms/L; kidney function normal; no infection in the urine.",
                "Referred to the hospital urology team on the suspected cancer route for an MRI scan; explained to the patient"),
            new("Examination: smooth, slightly enlarged prostate.",
                "PSA 4.9 micrograms/L, down from 6.2 after a urine infection was treated; kidney function normal.",
                "PSA falling after the infection: repeat in three months via the GP; no more tests now"),
        ],
        UrologyFollowUps, [], []);

    public static readonly ProcedureKind UrinarySymptoms = new(
        "Urinary symptoms", "the urinary symptoms", "PSA and kidney function", "the urology results meeting", null,
        ["getting up four times a night to pass urine", "a slow stream and hesitancy, getting worse", "urgency with occasional leaks"],
        [], [],
        [
            new("Symptom score 19 (moderate). Flow test: peak flow 9 ml/s; 110 ml left in the bladder on the scan. Smooth, enlarged prostate.",
                "PSA 2.1 micrograms/L; kidney function normal; urine clear.",
                "An alpha-blocker started via the GP; fluid and caffeine advice given; review in three months"),
            new("Symptom score 12 (mild to moderate). Flow test: peak flow 14 ml/s; bladder emptying well.",
                "PSA 1.4 micrograms/L; kidney function normal; a trace of blood in the urine, clear on repeat.",
                "Bladder training and evening fluid advice; no tablets for now"),
        ],
        UrologyFollowUps, [], []);

    private const string DiabetesSample = "HbA1c, kidney function and cholesterol";

    public static readonly ProcedureKind Type2Diabetes = new(
        "Type 2 diabetes review", "diabetes", DiabetesSample, "the diabetes team", null,
        ["the annual review, with an HbA1c above target at the GP", "a rising HbA1c despite two tablets", "a review after a change of treatment"],
        [], [],
        [
            new("Feet: pulses present, feeling intact to the monofilament. Eye screening up to date. Weight up 2 kg since last year.",
                "HbA1c 64 mmol/mol (was 58); kidney function stable; urine albumin normal; cholesterol 4.6.",
                "Target HbA1c of 53 agreed; the second tablet to be adjusted by the pharmacist; referred to the education course"),
            new("Feet: low risk. Eye screening due next month. Home readings 6 to 9.",
                "HbA1c 52 mmol/mol; kidney function normal; urine albumin slightly raised.",
                "Good control, so no change; repeat urine albumin in three months via the GP"),
            new("Feet: reduced feeling at two sites on the left; pulses present. Thirst and tiredness for a month.",
                "HbA1c 78 mmol/mol; kidney function normal; urine albumin normal.",
                "Treatment to be stepped up this week; foot protection advice given, and a podiatry review arranged"),
        ],
        ["Home readings 5 to 8 this fortnight; no hypos.", "Tolerating the new tablet; the mild thirst has settled.", "Went to the first education session."],
        ["Metformin increased to 1 g twice a day, as the slow-release tablet.", "Dapagliflozin 10 mg started; sick-day rules explained.", "Gliclazide reduced after two hypos before lunch.", "Statin restarted at a lower dose; to report any muscle aches."],
        []);

    public static readonly ProcedureKind Type1Diabetes = new(
        "Type 1 diabetes review", "diabetes", DiabetesSample, "the diabetes team", null,
        ["hypos overnight twice a week", "a review after starting a glucose sensor"],
        [], [],
        [
            new("Sensor data: 58% of the time in range, 6% below it, mostly from 02:00 to 04:00. Injection sites healthy.",
                "HbA1c 61 mmol/mol; kidney function normal; urine albumin normal.",
                "Evening background insulin reduced by 2 units; a carbohydrate counting refresher booked"),
            new("Sensor data: 71% of the time in range, 2% below it. Injection sites healthy; feet low risk.",
                "HbA1c 55 mmol/mol; kidney function normal; urine albumin normal.",
                "Doing well, so no change to doses; sensor alarms adjusted for overnight"),
        ],
        ["No overnight hypos for two weeks.", "Time in range up to 66%.", "One hypo after exercise: snack advice repeated."],
        ["Background insulin reduced by 2 units at night.", "Breakfast insulin ratio changed to 1 unit for 8 g of carbohydrate.", "Glucagon and hypo treatments checked: both in date."],
        []);

    private static readonly string[] Feet = ["left foot", "right foot", "both feet"];

    public static readonly ProcedureKind HeelPain = new(
        "Heel pain", "heel pain", "", "", null,
        ["heel pain on the first steps each morning for four months", "heel pain after long shifts on a hard floor"],
        [], [],
        [
            new("Tender on the inside of the heel; tight calf (the ankle bends 4 degrees past neutral with the knee straight).",
                "Goals met: first steps of the day pain-free; walking 5 km.",
                "Calf and sole stretches three times a day; gel heel pads now, with insoles to be fitted at the first session"),
        ],
        [], [], ["left heel", "right heel", "both heels"]);

    public static readonly ProcedureKind FlatFeet = new(
        "Flat feet with knee pain", "foot and knee pain", "", "", null,
        ["knee pain on stairs, with flat feet", "aching arches after standing all day", "flat feet, with tired legs after sport"],
        [], [],
        [
            new("Flexible flat feet; the heels roll in on walking. The knee drifts inwards on a single-leg squat.",
                "Goals met: stairs without pain; back to five-a-side football.",
                "Arch-support insoles and a hip-strength programme; footwear advice given"),
        ],
        [], [], Feet);

    public static readonly ProcedureKind ForefootPain = new(
        "Forefoot pain", "forefoot pain", "", "", null,
        ["pain under the ball of the foot when running", "burning between the toes in tight shoes"],
        [], [],
        [
            new("Tender under the second and third toe joints, with hard skin there. High arch.",
                "Goals met: running 5 km without pain.",
                "Dome-pad insoles; shoes with a wider toe box; a gradual return to running"),
        ],
        [], [], Feet);

    public static readonly ProcedureKind AchillesPain = new(
        "Achilles tendon pain", "Achilles pain", "", "", null,
        ["Achilles pain at the start of every run for three months", "a stiff, sore Achilles each morning"],
        [], [],
        [
            new("Thickened, tender middle part of the tendon; painful by the tenth single-leg heel raise.",
                "Goals met: 25 single-leg heel raises without pain.",
                "A heel-raise loading programme twice a day; heel lifts in both shoes for four weeks"),
        ],
        [], [], ["left ankle", "right ankle"]);

    /// <summary>The kinds of referral each pathway takes (some twice, as they're commoner).</summary>
    public static IReadOnlyList<ProcedureKind> For(Pathway pathway) => pathway switch
    {
        Pathway.SkinLesion or Pathway.SkinLesion2024 => [PigmentedLesion, PigmentedLesion, NonHealingLesion, NonHealingLesion, ScalyPatch],
        Pathway.InflammatorySkin => [Eczema, Eczema, Psoriasis, Psoriasis, Acne],
        Pathway.PostMenopausalBleeding => [PostMenopausalBleed, PostMenopausalBleed, PostMenopausalBleed, BleedingOnHrt],
        Pathway.Prostate => [RaisedPsa, RaisedPsa, UrinarySymptoms],
        Pathway.Diabetes => [Type2Diabetes, Type2Diabetes, Type2Diabetes, Type1Diabetes],
        Pathway.Biomechanics => [HeelPain, FlatFeet, ForefootPain, AchillesPain],
        _ => [],
    };

    /// <summary>Whether a pathway sees children (most of the centre's work is adults only).</summary>
    public static bool SeesChildren(Pathway pathway) => pathway is Pathway.InflammatorySkin or Pathway.Biomechanics;

    /// <summary>
    /// Whom a pathway sees: women only or men only (null for anyone), and from what age. The demo's patients have no
    /// recorded sex, only invented first names, so this keeps the names plausible for the two pathways where it shows.
    /// </summary>
    public static (bool? Female, int MinimumAge) Sees(Pathway pathway) => pathway switch
    {
        Pathway.PostMenopausalBleeding => (true, 50),
        Pathway.Prostate => (false, 50),
        _ => (null, 0),
    };
}
