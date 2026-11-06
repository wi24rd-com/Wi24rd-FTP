#!/bin/sh
# fast-tid.sh – OPSEC for git: alle commits får dagens dato + et fast klokkeslæt (standard 23:30),
# så commit-historikken på GitHub aldrig røber, hvornår der faktisk blev arbejdet.
#
#   sh tools/fast-tid.sh install   # lægger post-commit og pre-push hooks i dette repo
#   sh tools/fast-tid.sh check     # viser commits, der endnu ikke er pushet, og om de har fast tid
#   sh tools/fast-tid.sh ret       # retter ikke-pushede commits (fx efter rebase/cherry-pick) til fast tid
#
# Klokkeslættet kan ændres pr. repo: git config fasttid.klokken 12:00:00
# Begrænsninger: commits lavet på github.com (web-editor, squash/merge af PR) får GitHubs tid,
# og GitHub viser stadig push-tidspunkt i aktivitetsfeed, Actions og API'ets pushed_at.
set -eu

klokken() { git config --get fasttid.klokken 2>/dev/null || echo "23:30:00"; }
ok_tid() { # $1 = sha → 0 hvis både author og committer har det faste klokkeslæt
  k=$(klokken)
  a=$(git log -1 --format=%ad --date=format:%H:%M:%S "$1")
  c=$(git log -1 --format=%cd --date=format:%H:%M:%S "$1")
  [ "$a" = "$k" ] && [ "$c" = "$k" ]
}
upstream() { git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || true; }

case "${1:-}" in
  post-commit)
    [ -n "${FAST_TID_AKTIV:-}" ] && exit 0
    gd=$(git rev-parse --git-dir)
    # Under rebase/cherry-pick/merge rettes der ikke her – brug 'ret' bagefter.
    [ -d "$gd/rebase-merge" ] || [ -d "$gd/rebase-apply" ] || [ -f "$gd/CHERRY_PICK_HEAD" ] || [ -f "$gd/MERGE_HEAD" ] && exit 0
    ok_tid HEAD && exit 0
    dag=$(git log -1 --format=%cd --date=format:%Y-%m-%d HEAD)
    ts="$dag $(klokken)"
    FAST_TID_AKTIV=1 GIT_COMMITTER_DATE="$ts" git commit --amend --no-edit --no-verify --allow-empty --date="$ts" >/dev/null
    ;;
  pre-push)
    # stdin: <lokal ref> <lokal sha> <remote ref> <remote sha>
    nul=0000000000000000000000000000000000000000
    fejl=0
    while read -r lref lsha rref rsha; do
      [ "$lsha" = "$nul" ] && continue
      if [ "$rsha" = "$nul" ]; then interval="$lsha --not --remotes"; else interval="$rsha..$lsha"; fi
      for s in $(git rev-list $interval); do
        if ! ok_tid "$s"; then echo "fast-tid: $(git log -1 --format='%h %ad  %s' --date=format:'%Y-%m-%d %H:%M' "$s")" >&2; fejl=1; fi
      done
    done
    if [ "$fejl" = 1 ]; then
      echo "fast-tid: push stoppet – commits ovenfor har rigtigt klokkeslæt. Kør: sh tools/fast-tid.sh ret" >&2
      exit 1
    fi
    ;;
  install)
    gd=$(git rev-parse --git-dir); mkdir -p "$gd/hooks"
    for h in post-commit pre-push; do
      if [ -f "$gd/hooks/$h" ] && ! grep -q fast-tid "$gd/hooks/$h"; then echo "Findes allerede: $gd/hooks/$h – flet manuelt." >&2; exit 1; fi
      printf '#!/bin/sh\n# fast-tid (se tools/fast-tid.sh)\nexec sh "$(git rev-parse --show-toplevel)/tools/fast-tid.sh" %s "$@"\n' "$h" > "$gd/hooks/$h"
      chmod +x "$gd/hooks/$h"
    done
    echo "fast-tid installeret – commits får klokken $(klokken)."
    ;;
  check)
    u=$(upstream); interval=${u:+$u..HEAD}; interval=${interval:-HEAD --not --remotes}
    git rev-list $interval | while read -r s; do
      if ok_tid "$s"; then m=ok; else m=RET; fi
      echo "$m  $(git log -1 --format='%h %ad  %s' --date=format:'%Y-%m-%d %H:%M' "$s")"
    done
    ;;
  ret)
    u=$(upstream)
    [ -z "$u" ] && { echo "Ingen upstream – angiv base: git rebase --committer-date-is-author-date <base> efter at have sat author-dato." >&2; exit 1; }
    k=$(klokken)
    # Sæt author- og committer-dato på hver ikke-pushet commit til dens egen dag + fast klokkeslæt.
    FAST_TID_AKTIV=1 git rebase -q "$u" --exec "FAST_TID_AKTIV=1 sh -c 'd=\$(git log -1 --format=%ad --date=format:%Y-%m-%d); GIT_COMMITTER_DATE=\"\$d $k\" git commit --amend --no-edit --no-verify --allow-empty --date=\"\$d $k\" >/dev/null'"
    echo "Rettet. Tjek med: sh tools/fast-tid.sh check"
    ;;
  *) sed -n '2,13p' "$0"; exit 1 ;;
esac
