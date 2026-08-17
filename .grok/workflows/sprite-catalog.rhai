let meta = #{
    name: "sprite-catalog",
    description: "Implement the Art sprite catalog in three slices; each slice is implement then adversarially verify until pass",
    when_to_use: "Building or continuing the static Sprites.cs / SpriteGroup catalog for client/Assets/Art",
    phases: [
        #{ title: "Inspector", detail: "kind/group/time/color module + userData" },
        #{ title: "Generator", detail: "Tools.SpriteBuilder + generated group classes" },
        #{ title: "Runtime", detail: "LoadSpriteGroup + lifetime unload" },
        #{ title: "Audit", detail: "final spec compliance" },
    ],
};

let verdict_schema = #{
    "type": "object",
    "required": ["pass", "evidence", "findings"],
    "properties": #{
        "pass": #{ "type": "boolean" },
        "evidence": #{ "type": "string" },
        "findings": #{
            "type": "array",
            "maxItems": 12,
            "items": #{
                "type": "object",
                "required": ["file", "issue", "severity"],
                "properties": #{
                    "file": #{ "type": "string" },
                    "issue": #{ "type": "string" },
                    "severity": #{ "type": "string" },
                },
            },
        },
    },
};

let audit_schema = #{
    "type": "object",
    "required": ["pass", "summary"],
    "properties": #{
        "pass": #{ "type": "boolean" },
        "summary": #{ "type": "string" },
    },
};

let autonomous = false;
let only_slice = "";
if args != () {
    if args.mode != () && args.mode == "autonomous" {
        autonomous = true;
    }
    if args.slice != () {
        only_slice = args.slice;
    }
}

fn block_findings(findings) {
    let out = [];
    if findings == () {
        return out;
    }
    for f in findings {
        if f != () && f.severity != () && f.severity == "block" {
            out.push(f);
        }
    }
    out
}

fn verdict_usable(result) {
    result != () && result.success && result.output != () && result.output.pass != () && result.output.evidence != ()
}

fn implement_prompt(slice_id, slice_title) {
    let p = "";
    p += "You are the implementer for the mines-leader Unity sprite catalog.\n";
    p += "Repo root: /projects/mines-leader\n";
    p += "Read these files with read_file before editing anything:\n";
    p += "1. /projects/mines-leader/docs/tasks/current/sprite_catalog/spec.md\n";
    p += "2. /projects/mines-leader/docs/tasks/current/sprite_catalog/progress.md\n";
    p += "3. client/Assets/Tools/Editor/SceneBuilder/SceneGenerator.cs\n";
    p += "4. client/Assets/Tools/Editor/SceneBuilder/ScenesClassGenerator.cs\n";
    p += "5. client/Assets/Tools/Runtime/SceneBuilder/StaticScene.cs\n";
    p += "6. client/Assets/Internal/Scopes/Services/Builder/ScopeBuilderExtensions.cs\n";
    p += "7. client/Assets/Common/Animations/Sprites/SpriteAnimationData.cs\n";
    p += "Do not answer from memory. The spec is authoritative.\n\n";
    p += "CURRENT SLICE: ";
    p += slice_id;
    p += " - ";
    p += slice_title;
    p += "\nImplement ONLY this slice. Do not start later slices.\n";
    p += "Follow CODE_STYLE (braces same line, _camelCase, no Async suffix on UniTask).\n";
    p += "EnsureLoaded must only throw - never load.\n";
    p += "Do not replace TextureImporter editors. Use Editor.finishedDefaultHeaderGUI.\n";
    p += "Do not create an assembly reference cycle (see spec).\n";
    p += "After you finish, append what you changed to /projects/mines-leader/docs/tasks/current/sprite_catalog/progress.md\n";
    p += "and mark the slice in the snapshot table.\n";
    p
}

fn verify_prompt(slice_id, slice_title) {
    let p = "";
    p += "You are an adversarial critic. Your job is to REFUTE the implementer, not help them.\n";
    p += "Repo: /projects/mines-leader\n";
    p += "Read the spec in full: /projects/mines-leader/docs/tasks/current/sprite_catalog/spec.md\n";
    p += "Read progress: /projects/mines-leader/docs/tasks/current/sprite_catalog/progress.md\n";
    p += "Then grep and read_file the files that should exist for slice ";
    p += slice_id;
    p += " (";
    p += slice_title;
    p += ").\n";
    p += "Do not answer from memory. An empty findings list is valid ONLY after you have actually opened the new/changed files.\n";
    p += "Set pass=true only if there are ZERO findings with severity \"block\" for this slice's acceptance checklist.\n";
    p += "severity must be \"block\" or \"nit\". nits do not fail the slice.\n";
    p += "If a required file is missing, that is a block.\n";
    p += "If EnsureLoaded loads, or a getter calls Addressables, or TextureImporter is replaced, that is a block.\n";
    p += "evidence must cite concrete paths you read.\n";
    p += "Current slice only - do not fail this slice for work that belongs to a later slice.\n";
    p
}

fn fix_prompt(slice_id, findings_json) {
    let p = "";
    p += "The critic rejected slice ";
    p += slice_id;
    p += ". Spec is still authoritative: /projects/mines-leader/docs/tasks/current/sprite_catalog/spec.md\n";
    p += "Read the listed files and fix every finding with severity \"block\". Leave nits unless they are one-line.\n";
    p += "Do not start another slice. Do not expand scope.\n";
    p += "EnsureLoaded must only throw.\n";
    p += "Findings JSON:\n";
    p += findings_json;
    p += "\nAfter fixes, update /projects/mines-leader/docs/tasks/current/sprite_catalog/progress.md\n";
    p
}

fn run_slice(slice_id, slice_title, phase_title, is_auto, schema) {
    phase(phase_title);
    log("slice " + slice_id + " implement");

    let impl = agent(implement_prompt(slice_id, slice_title), #{
        label: "impl:" + slice_id,
        capability_mode: "all",
        agent_type: "general-purpose",
    });

    if impl == () || impl.success == false {
        log("implementer failed for " + slice_id);
        if is_auto {
            return #{ id: slice_id, status: "impl-failed" };
        }
        await_user("verification", "Implementer failed on slice " + slice_id + ". Resume after you decide how to continue.");
        return #{ id: slice_id, status: "impl-failed" };
    }

    let last_fp = "";
    let round = 0;
    let last_status = "unverified";

    while round < 4 {
        round += 1;
        log("slice " + slice_id + " verify round " + round.to_string());

        let critic = agent(verify_prompt(slice_id, slice_title), #{
            label: "verify:" + slice_id + ":" + round.to_string(),
            capability_mode: "read-only",
            agent_type: "general-purpose",
            output_schema: schema,
        });

        if verdict_usable(critic) == false {
            log("critic returned an unusable verdict on " + slice_id);
            if is_auto {
                return #{ id: slice_id, status: "critic-unusable" };
            }
            await_user("verification", "Critic verdict unusable on slice " + slice_id + ". Resume to retry or skip.");
            return #{ id: slice_id, status: "critic-unusable" };
        }

        let findings = critic.output.findings;
        if findings == () {
            findings = [];
        }
        let blocks = block_findings(findings);
        let fp = fingerprint(json_encode(blocks));

        if critic.output.pass == true && blocks.len() == 0 {
            log("slice " + slice_id + " PASS");
            return #{ id: slice_id, status: "pass", rounds: round };
        }

        if last_fp != "" && fp == last_fp {
            log("slice " + slice_id + " stalled");
            if is_auto {
                return #{ id: slice_id, status: "stalled", rounds: round };
            }
            await_user("no_progress", "Slice " + slice_id + " stalled on the same block findings. Resume after you resolve the deadlock.");
            return #{ id: slice_id, status: "stalled", rounds: round };
        }

        last_fp = fp;
        last_status = "rejected";

        log("slice " + slice_id + " fix " + blocks.len().to_string() + " block(s)");
        let impl2 = agent(fix_prompt(slice_id, json_encode(blocks)), #{
            label: "fix:" + slice_id + ":" + round.to_string(),
            capability_mode: "all",
            agent_type: "general-purpose",
            resume_from: impl.agent_id,
        });
        if impl2 != () && impl2.success {
            impl = impl2;
        }
    }

    log("slice " + slice_id + " hit max rounds");
    if is_auto {
        return #{ id: slice_id, status: "max-rounds" };
    }
    await_user("verification", "Slice " + slice_id + " still failing after 4 rounds. Resume after you decide.");
    #{ id: slice_id, status: "max-rounds" }
}

let results = [];

if only_slice == "" || only_slice == "inspector" {
    results.push(run_slice("inspector", "Inspector + metadata", "Inspector", autonomous, verdict_schema));
}
if only_slice == "" || only_slice == "generator" {
    results.push(run_slice("generator", "Runtime types + generator", "Generator", autonomous, verdict_schema));
}
if only_slice == "" || only_slice == "runtime" {
    results.push(run_slice("runtime", "LoadSpriteGroup + lifetime", "Runtime", autonomous, verdict_schema));
}

phase("Audit");
log("final audit");

let audit_prompt = "";
audit_prompt += "Read-only final audit of the sprite catalog work.\n";
audit_prompt += "Spec: /projects/mines-leader/docs/tasks/current/sprite_catalog/spec.md\n";
audit_prompt += "Progress: /projects/mines-leader/docs/tasks/current/sprite_catalog/progress.md\n";
audit_prompt += "Repo: /projects/mines-leader\n";
audit_prompt += "Read the spec forbidden list and every slice acceptance checklist.\n";
audit_prompt += "Use grep and read_file. Do not answer from memory.\n";
audit_prompt += "pass=true only if no block-level spec violations remain in the tree.\n";
audit_prompt += "summary: short paragraph of what exists and what is still missing.\n";

let audit = agent(audit_prompt, #{
    label: "audit",
    capability_mode: "read-only",
    agent_type: "general-purpose",
    output_schema: audit_schema,
});

let audit_pass = false;
let audit_summary = "audit did not return a usable result";
if audit != () && audit.success && audit.output != () && audit.output.pass != () {
    audit_pass = audit.output.pass;
    if audit.output.summary != () {
        audit_summary = audit.output.summary;
    }
}

let report = "";
report += "# sprite-catalog run\n\n";
for r in results {
    if r != () {
        report += "- ";
        report += r.id;
        report += ": ";
        report += r.status;
        report += "\n";
    }
}
report += "\nAudit pass: ";
if audit_pass {
    report += "true";
} else {
    report += "false";
}
report += "\n\n";
report += audit_summary;
report += "\n";

let report_path = write_scratch_file("result.md", report);

if audit_pass == false && autonomous == false {
    await_user("verification", "Final audit did not pass. Resume after you inspect /workflows.");
}

complete(#{
    path: report_path,
    report: report,
    slices: results,
    audit_pass: audit_pass,
});
