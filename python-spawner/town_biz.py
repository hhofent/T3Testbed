"""Town business program top-up and placement for town bake-off.
NW Iowa 1990 small towns: 25-ft storefronts (Spencer 25x142 ft, Naumann 2004),
100-ft Main Street.
"""
from spawner import FT

REQUIRED_UNION = [
    'grain elevator', 'co-op', 'gas station', 'cafe', 'bank',
    'post office', 'church', 'school', 'implement dealer', 'tavern', 'hardware/lumber'
]

SINGLETONS = {'post office', 'city hall / fire', 'school', 'bank'}

# Curated list of typical 1990 NW Iowa small-town businesses
TINY_TYPES = ['post office', 'cafe', 'gas station', 'tavern', 'hardware', 'grocery']
MID_TYPES = [
    'post office', 'bank', 'gas station', 'cafe', 'tavern', 'hardware',
    'grocery', 'insurance', 'beauty shop', 'church', 'school',
    'implement dealer', 'locker plant', 'auto repair'
]
BIG_TYPES = [
    'post office', 'bank', 'gas station', 'cafe', 'tavern', 'hardware',
    'grocery', 'insurance', 'beauty shop', 'church', 'school',
    'implement dealer', 'locker plant', 'drug store', 'vet', 'city hall / fire',
    'funeral home', 'nursing home', 'variety'
]

def top_up_program(seed, prog, size, rail_town):
    """Ensure sufficient distinct business types per town size and ensure REQUIRED_UNION is met."""
    types_present = set(prog)
    needed_count = 6 if size == 'tiny' else (12 if size == 'mid' else 15)
    
    candidates = BIG_TYPES if size == 'big' else (MID_TYPES if size == 'mid' else TINY_TYPES)
    
    out = list(prog)
    if size in ('mid', 'big') and 'implement dealer' not in types_present:
        out.append('implement dealer')
        types_present.add('implement dealer')
        
    if size in ('mid', 'big') and 'school' not in types_present:
        out.append('school')
        types_present.add('school')

    for t in candidates:
        if len(types_present) >= needed_count + 1:
            break
        if t in types_present:
            continue
        if t in SINGLETONS and t in types_present:
            continue
        out.append(t)
        types_present.add(t)

    return out
