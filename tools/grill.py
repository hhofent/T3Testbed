#!/usr/bin/env python3
"""
Grill CLI — Manage grilled Q&A sessions.
Usage:
  python tools/grill.py add --topic "Topic" --question "Q" --answer "A" [--tags "tag1,tag2"] [--params "Param1,Param2"]
  python tools/grill.py search --tag "noise"
  python tools/grill.py list
  python tools/grill.py show G-001
"""
import sys
import json
import yaml
import argparse
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).parent.parent
DATA_FILE = ROOT / "docs" / "research" / "GRILLED_DATA.md"
INDEX_FILE = ROOT / "docs" / "research" / "GRILL_INDEX.json"

def load_index():
    if INDEX_FILE.exists():
        with open(INDEX_FILE) as f:
            return json.load(f)
    return {"entries": [], "tags": {}, "params": {}}

def save_index(idx):
    with open(INDEX_FILE, 'w') as f:
        json.dump(idx, f, indent=2)

def next_id(idx):
    nums = [int(e['id'].split('-')[1]) for e in idx['entries'] if e['id'].startswith('G-')]
    return f"G-{max(nums, default=0)+1:03d}"

def cmd_add(args):
    idx = load_index()
    eid = next_id(idx)
    
    tags = [t.strip() for t in args.tags.split(',')] if args.tags else []
    params = [p.strip() for p in args.params.split(',')] if args.params else []
    
    entry = {
        "id": eid,
        "topic": args.topic,
        "date": datetime.now().isoformat()[:10],
        "status": "complete",
        "tags": tags,
        "params": params,
        "question": args.question,
        "answer": args.answer,
        "decisions": args.decisions.split('|') if args.decisions else [],
        "open_questions": args.open.split('|') if args.open else [],
    }
    
    # Append to markdown
    md_entry = f"""---
id: {eid}
topic: "{args.topic}"
date: "{entry['date']}"
status: "complete"
tags: {tags}
params: {params}
---
## Question
{args.question}

## Answer
{args.answer}

## Decisions
{chr(10).join('- ' + d for d in entry['decisions']) if entry['decisions'] else '- (none)'}

## Open Questions
{chr(10).join('- ' + q for q in entry['open_questions']) if entry['open_questions'] else '- (none)'}

## Related Params
{chr(10).join('- ' + p for p in params) if params else '- (none)'}
"""
    
    with open(DATA_FILE, 'a') as f:
        if DATA_FILE.stat().st_size > 0:
            f.write('\n\n---\n\n')
        f.write(md_entry)
    
    # Update index
    idx['entries'].append({"id": eid, "topic": args.topic, "date": entry['date'], "tags": tags, "params": params})
    for t in tags:
        idx['tags'].setdefault(t, []).append(eid)
    for p in params:
        idx['params'].setdefault(p, []).append(eid)
    save_index(idx)
    
    print(f"Added {eid}: {args.topic}")

def cmd_search(args):
    idx = load_index()
    results = []
    for e in idx['entries']:
        if args.tag and args.tag not in e['tags']:
            continue
        if args.param and args.param not in e['params']:
            continue
        if args.topic and args.topic.lower() not in e['topic'].lower():
            continue
        results.append(e)
    
    for e in results:
        print(f"{e['id']} | {e['date']} | {e['topic']} | tags: {','.join(e['tags'])}")

def cmd_list(args):
    idx = load_index()
    for e in idx['entries']:
        print(f"{e['id']} | {e['date']} | {e['topic']}")

def cmd_show(args):
    if not DATA_FILE.exists():
        print("No data file")
        return
    content = DATA_FILE.read_text()
    # Find entry by id
    parts = content.split('\n---\n\n')
    for p in parts:
        if p.startswith(f"id: {args.id}") or p.startswith(f"id: {args.id}\n"):
            print(p.strip())
            return
    print(f"Entry {args.id} not found")

def main():
    parser = argparse.ArgumentParser(description="Grill CLI")
    sub = parser.add_subparsers(dest='cmd', required=True)
    
    p_add = sub.add_parser('add')
    p_add.add_argument('--topic', required=True)
    p_add.add_argument('--question', required=True)
    p_add.add_argument('--answer', required=True)
    p_add.add_argument('--tags', default='')
    p_add.add_argument('--params', default='')
    p_add.add_argument('--decisions', default='')
    p_add.add_argument('--open', default='')
    p_add.set_defaults(func=cmd_add)
    
    p_search = sub.add_parser('search')
    p_search.add_argument('--tag', default='')
    p_search.add_argument('--param', default='')
    p_search.add_argument('--topic', default='')
    p_search.set_defaults(func=cmd_search)
    
    p_list = sub.add_parser('list')
    p_list.set_defaults(func=cmd_list)
    
    p_show = sub.add_parser('show')
    p_show.add_argument('id')
    p_show.set_defaults(func=cmd_show)
    
    args = parser.parse_args()
    args.func(args)

if __name__ == '__main__':
    main()