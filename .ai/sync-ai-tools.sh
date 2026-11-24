#!/usr/bin/env bash
# sync-ai-tools.sh — Syncs .ai/skills and .ai/rules to .claude/ and .github/
#
# On macOS/Linux: creates relative symlinks (zero duplication).
# On Windows (Git Bash): copies files/directories (symlinks need Developer Mode).
#
# Targets:
#   .claude/skills/<name>       → .ai/skills/<name>
#   .claude/rules/<name>.md     → .ai/rules/<name>.md
#   .github/skills/<name>       → .ai/skills/<name>
#   .github/instructions/<name>.instructions.md → .ai/rules/<name>.md
#
# Usage:
#   bash .ai/sync-ai-tools.sh      # from repo root (or any directory)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

AI_SKILLS="$REPO_ROOT/.ai/skills"
AI_RULES="$REPO_ROOT/.ai/rules"

CLAUDE_SKILLS="$REPO_ROOT/.claude/skills"
CLAUDE_RULES="$REPO_ROOT/.claude/rules"
GITHUB_SKILLS="$REPO_ROOT/.github/skills"
GITHUB_INSTRUCTIONS="$REPO_ROOT/.github/instructions"

VSCODE_SETTINGS_DIR="$REPO_ROOT/.vscode"
VSCODE_SETTINGS_FILE="$VSCODE_SETTINGS_DIR/settings.json"

# --- Platform detection ---------------------------------------------------
USE_COPIES=false
case "${OSTYPE:-}" in
    msys*|cygwin*) USE_COPIES=true ;;
esac
if ! $USE_COPIES; then
    case "$(uname -s 2>/dev/null)" in
        MINGW*|MSYS*|CYGWIN*) USE_COPIES=true ;;
    esac
fi

# --- Frontmatter helpers --------------------------------------------------

# Extract globs array from frontmatter, one item per line.
# Handles: globs: ["a", "b"] or globs: []
parse_globs() {
    local file="$1"
    awk '
        /^---$/ { if(++n==1) next; exit }
        n==1 && /^globs:/ {
            sub(/^globs:[[:space:]]*/,"")
            gsub(/[\[\]]/,"")
            cnt = split($0, a, ",")
            for (i=1; i<=cnt; i++) {
                item = a[i]
                gsub(/^[[:space:]"]+|[[:space:]"]+$/, "", item)
                if (item != "") print item
            }
        }
    ' "$file"
}

# Returns success when frontmatter contains: globs: []
has_empty_globs_array() {
    local file="$1"
    awk '
        /^---$/ { if(++n==1) next; exit }
        n==1 && /^globs:[[:space:]]*\[[[:space:]]*\][[:space:]]*$/ { found=1 }
        END { exit(found ? 0 : 1) }
    ' "$file"
}

# Print everything after the closing --- of the frontmatter.
# If no frontmatter, prints the whole file.
get_body() {
    local file="$1"
    awk '
        BEGIN { state=0 }
        state==0 && /^---$/ { state=1; next }
        state==0 { print; next }
        state==1 && /^---$/ { state=2; next }
        state==2 { print }
    ' "$file"
}

# Write a Claude rule file: strips frontmatter, adds paths: from globs.
write_claude_rule() {
    local src="$1" dst="$2"
    local globs
    globs=$(parse_globs "$src")
    {
        if [ -n "$globs" ]; then
            echo "---"
            echo "paths:"
            while IFS= read -r g; do
                [ -n "$g" ] && echo "  - \"$g\""
            done <<< "$globs"
            echo "---"
        elif has_empty_globs_array "$src"; then
            echo "---"
            echo "paths:"
            echo "  - \"**\""
            echo "---"
        fi
        get_body "$src"
    } > "$dst"
}

# Write a Copilot instructions file: strips frontmatter, adds applyTo: from globs.
write_copilot_rule() {
    local src="$1" dst="$2"
    local globs
    globs=$(parse_globs "$src")
    {
        if [ -n "$globs" ]; then
            echo "---"
            local joined
            joined=$(echo "$globs" | tr '\n' ',' | sed 's/,$//')
            echo "applyTo: \"$joined\""
            echo "---"
        elif has_empty_globs_array "$src"; then
            echo "---"
            echo "applyTo: \"**\""
            echo "---"
        fi
        get_body "$src" | sed -E 's@\]\((\./)?([[:alnum:]_-]+)\.md([#)])@](\1\2.instructions.md\3@g'
    } > "$dst"
}

# --- Cleanup helpers ------------------------------------------------------

# Remove symlinks whose target contains $pattern (macOS / Linux).
remove_managed_symlinks() {
    local target_dir="$1"
    local pattern="$2"
    [ -d "$target_dir" ] || return 0
    for link in "$target_dir"/*; do
        [ -L "$link" ] || continue
        local resolved
        resolved="$(readlink "$link")"
        if [[ "$resolved" == *"$pattern"* ]]; then
            rm "$link"
        fi
    done
}

# Remove copies whose basename matches an entry in $source_dir (Windows).
remove_managed_copies() {
    local target_dir="$1"
    local source_dir="$2"
    [ -d "$target_dir" ] || return 0
    for entry in "$target_dir"/*; do
        [ -e "$entry" ] || continue
        local name
        name="$(basename "$entry")"
        # Strip .instructions.md suffix added for GitHub instructions
        local check="${name%.instructions.md}"
        # Match against any file/dir in the source (with or without extension)
        if [ -e "$source_dir/$name" ] || [ -e "$source_dir/$check" ]; then
            rm -rf "$entry"
        else
            # Try matching with common extensions (.md, .mdc)
            for ext in md mdc; do
                if [ -e "$source_dir/$check.$ext" ]; then
                    rm -rf "$entry"
                    break
                fi
            done
        fi
    done
}

# Dispatcher — picks the right cleanup strategy for the current OS.
remove_managed() {
    if $USE_COPIES; then
        remove_managed_copies "$1" "$2"
    else
        # $2 is source_dir for copies but pattern for symlinks — derive pattern
        local pattern
        pattern="$(echo "$2" | sed "s|$REPO_ROOT||")"
        remove_managed_symlinks "$1" "$pattern"
    fi
}

ensure_vscode_settings() {
    if [ "${TERM_PROGRAM:-}" != "vscode" ] && [ -z "${VSCODE_PID:-}" ]; then
        return 0
    fi

    mkdir -p "$VSCODE_SETTINGS_DIR"

    # Merge, never overwrite: keep existing keys (e.g. WASM debug config) and
    # comments; only add the chat.* keys when they are missing.
    if [ -f "$VSCODE_SETTINGS_FILE" ] \
        && grep -q 'chat.instructionsFilesLocations' "$VSCODE_SETTINGS_FILE" \
        && grep -q 'chat.agentSkillsLocations' "$VSCODE_SETTINGS_FILE"; then
        return 0
    fi

    local existing=""
    if [ -f "$VSCODE_SETTINGS_FILE" ]; then
        existing="$(cat "$VSCODE_SETTINGS_FILE")"
    fi
    if [ -z "$(printf '%s' "$existing" | tr -d '[:space:]')" ]; then
        existing="{
}"
    fi

    # Drop trailing whitespace and the final closing brace line.
    local body
    body="$(printf '%s\n' "$existing" | awk '
        { lines[NR] = $0 }
        END {
            last = NR
            while (last > 0 && lines[last] ~ /^[[:space:]]*$/) last--
            if (last > 0 && lines[last] ~ /^[[:space:]]*}[[:space:]]*$/) last--
            for (i = 1; i <= last; i++) print lines[i]
        }
    ')"

    case "$body" in
        *,) ;;        # already ends with a comma
        *"{") ;;     # empty object
        *) body="$body," ;;
    esac

    cat > "$VSCODE_SETTINGS_FILE" <<EOF
$body
    "chat.instructionsFilesLocations": {
        ".claude/rules": false,
        "~/.claude/rules": false
    },
    "chat.agentSkillsLocations": {
        "~/.claude/skills": false,
        ".claude/skills": false
    }
}
EOF
    echo "  vscode: settings.json updated (merged)"
}

# --- Main -----------------------------------------------------------------

echo "Syncing .ai/ skills and rules..."
echo ""

# Skills: symlinks or copies (unchanged)
remove_managed "$CLAUDE_SKILLS" "$AI_SKILLS"
remove_managed "$GITHUB_SKILLS" "$AI_SKILLS"

# Rules: always written files — clear existing files and symlinks before rewriting
remove_managed "$CLAUDE_RULES" "$AI_RULES"
remove_managed "$GITHUB_INSTRUCTIONS" "$AI_RULES"
find "$CLAUDE_RULES"        -maxdepth 1 \( -name "*.md" -o -type l \) -delete 2>/dev/null || true
find "$GITHUB_INSTRUCTIONS" -maxdepth 1 \( -name "*.md" -o -type l \) -delete 2>/dev/null || true

mkdir -p "$CLAUDE_SKILLS" "$CLAUDE_RULES" "$GITHUB_SKILLS" "$GITHUB_INSTRUCTIONS"

changed=0

for skill_dir in "$AI_SKILLS"/*/; do
    [ -d "$skill_dir" ] || continue
    name="$(basename "$skill_dir")"

    if $USE_COPIES; then
        cp -r "$AI_SKILLS/$name" "$CLAUDE_SKILLS/$name"
        cp -r "$AI_SKILLS/$name" "$GITHUB_SKILLS/$name"
    else
        ln -s "../../.ai/skills/$name" "$CLAUDE_SKILLS/$name"
        ln -s "../../.ai/skills/$name" "$GITHUB_SKILLS/$name"
    fi
    echo "  skill: $name"
    ((changed += 2))
done

for rule_file in "$AI_RULES"/*; do
    [ -f "$rule_file" ] || continue
    name="$(basename "$rule_file")"
    base="${name%.*}"

    write_claude_rule  "$rule_file" "$CLAUDE_RULES/$name"
    write_copilot_rule "$rule_file" "$GITHUB_INSTRUCTIONS/$base.instructions.md"
    echo "  rule:  $name"
    ((changed += 2))
done

if [ -d "$AI_RULES/references" ]; then
    if $USE_COPIES; then
        cp -r "$AI_RULES/references" "$CLAUDE_RULES/references"
        cp -r "$AI_RULES/references" "$GITHUB_INSTRUCTIONS/references"
    else
        ln -s "../../.ai/rules/references" "$CLAUDE_RULES/references"
        ln -s "../../.ai/rules/references" "$GITHUB_INSTRUCTIONS/references"
    fi
    ((changed += 2))
fi

ensure_vscode_settings

echo ""
if [ "$changed" -gt 0 ]; then
    echo "Done. $changed items synced."
else
    echo "Done. No skills or rules found in .ai/."
fi
