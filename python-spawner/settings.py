"""Generator settings. Geometry uses integer sixteenth-inch ticks unless stated otherwise."""
import copy
from contextlib import contextmanager
from threading import RLock
T = 16
def IN(x): return int(round(x*T))
def FT(x): return int(round(x*12*T))
DEFAULTS = {}
T = 16
DEFAULTS['spawner.t'] = T
SF = FT(1) ** 2
DEFAULTS['spawner.sf'] = SF
CLR = IN(4.5)
DEFAULTS['spawner.clr'] = CLR
HEAD = IN(83)
DEFAULTS['spawner.head'] = HEAD
GRID, MODULE = FT(2), FT(4)
DEFAULTS['spawner.grid'] = GRID
DEFAULTS['spawner.module'] = MODULE
HABITABLE = {"living", "kitchen", "dining", "family", "bedroom"}
DEFAULTS['spawner.habitable'] = HABITABLE
PUBLIC = {"living", "kitchen", "dining", "family"}
DEFAULTS['spawner.public'] = PUBLIC
STYLE = {1985: (47, 39, 13), 1990: (44, 45, 11), 1995: (44, 50, 6), 2000: (45, 50, 5)}
DEFAULTS['spawner.style'] = STYLE
BEDS = {1985: (24, 55, 20), 1990: (15, 57, 27), 1995: (17, 54, 28), 2000: (17, 53, 31)}
DEFAULTS['spawner.beds'] = BEDS
BATHS = {1990: (18, 82), 1995: (18, 82), 2000: (11, 89)}
DEFAULTS['spawner.baths'] = BATHS
FOUND = {1985: (77, 11, 13), 1990: (81, 8, 11), 1995: (79, 10, 11), 2000: (79, 13, 8)}
DEFAULTS['spawner.found'] = FOUND
GARAGE = {1985: (13, 13, 73), 1990: (9, 8, 83), 1995: (9, 6, 84), 2000: (7, 4, 88)}
DEFAULTS['spawner.garage'] = GARAGE
OUTDOOR = {1995: (42, 38, 27), 2000: (47, 36, 31)}
DEFAULTS['spawner.outdoor'] = OUTDOOR
GARAGE_CHAIN = {"double": ["double", "single", "detached"], "single": ["single", "detached"], "none": ["none"], "detached": ["detached"]}
DEFAULTS['spawner.garage_chain'] = GARAGE_CHAIN
GW = {"double": FT(22), "single": FT(12), "none": 0, "detached": 0}
DEFAULTS['spawner.gw'] = GW
TRAILER_ROOF = ["low-slope metal", "low-slope membrane", "shingled gable"]
DEFAULTS['spawner.trailer_roof'] = TRAILER_ROOF
TRAILER_SIDING = ["metal lap", "vinyl lap", "T1-11 plywood", "aluminum skirt+lap"]
DEFAULTS['spawner.trailer_siding'] = TRAILER_SIDING
TRAILER_W = [14, 16]
DEFAULTS['spawner.trailer_w'] = TRAILER_W
TRAILER_L = (60, 76)
DEFAULTS['spawner.trailer_l'] = TRAILER_L
TRAILER_NA = {"1": "door on the long side facing the pad, not the road end",
              "4": "no garage expected", "5": "no garage expected", "8": "no dining room or garage",
              "20": "factory-built; 14/16 ft widths are off the 4 ft module", "23": "single-wide area is about 840-1,200 sf",
              "24": "set on piers/skirting, no foundation draw", "25": "furnace/water heater closet, no utility room",
              "30": "no porch/deck drawn", "21": "style = single-wide"}
DEFAULTS['spawner.trailer_na'] = TRAILER_NA
SIDE_PCT, SIDE_MIN_PCT = 10, 5
DEFAULTS['spawner.side_pct'] = SIDE_PCT
DEFAULTS['spawner.side_min_pct'] = SIDE_MIN_PCT
FRONT_PCT, FRONT_MIN_PCT = 20, 15
DEFAULTS['spawner.front_pct'] = FRONT_PCT
DEFAULTS['spawner.front_min_pct'] = FRONT_MIN_PCT
REAR_PCT, REAR_MIN_PCT = 25, 20
DEFAULTS['spawner.rear_pct'] = REAR_PCT
DEFAULTS['spawner.rear_min_pct'] = REAR_MIN_PCT
CORNER_SIDE_X = 2
DEFAULTS['spawner.corner_side_x'] = CORNER_SIDE_X
PORCH_PROJECT_MAX_W = FT(70)
DEFAULTS['spawner.porch_project_max_w'] = PORCH_PROJECT_MAX_W
PORCH_PROJECT = FT(6)
DEFAULTS['spawner.porch_project'] = PORCH_PROJECT
MILE = FT(5280)
DEFAULTS['region.mile'] = MILE
N_MI = 3
DEFAULTS['region.n_mi'] = N_MI
ROW = FT(66)
DEFAULTS['region.row'] = ROW
SURF = {"gravel": (22, 24), "paved": (22, 22)}
DEFAULTS['region.surf'] = SURF
PAVED_PCT = (22, 25)
DEFAULTS['region.paved_pct'] = PAVED_PCT
TOWN_PCT = 35
DEFAULTS['region.town_pct'] = TOWN_PCT
CLEAR = (150, 300)
DEFAULTS['region.clear'] = CLEAR
FARM_PER_MILE = [(0, 15), (1, 50), (2, 35)]
DEFAULTS['region.farm_per_mile'] = FARM_PER_MILE
ACREAGE_PCT = 25
DEFAULTS['region.acreage_pct'] = ACREAGE_PCT
FARM_W, FARM_D, FARM_AC = range(300, 501, 50), range(400, 801, 50), (3, 9)
DEFAULTS['region.farm_w'] = FARM_W
DEFAULTS['region.farm_d'] = FARM_D
DEFAULTS['region.farm_ac'] = FARM_AC
FARM_BACK = (100, 300)
DEFAULTS['region.farm_back'] = FARM_BACK
ACRE_W, ACRE_D, ACRE_AC = range(150, 301, 25), range(250, 601, 50), (1, 5)
DEFAULTS['region.acre_w'] = ACRE_W
DEFAULTS['region.acre_d'] = ACRE_D
DEFAULTS['region.acre_ac'] = ACRE_AC
ACRE_BACK = 60
DEFAULTS['region.acre_back'] = ACRE_BACK
GROVE_W = FT(80)
DEFAULTS['region.grove_w'] = GROVE_W
TOWN_GRIDS = [(2, 2), (3, 2), (2, 3), (3, 3)]
DEFAULTS['region.town_grids'] = TOWN_GRIDS
BLOCK_CX = range(380, 446, 5)
DEFAULTS['region.block_cx'] = BLOCK_CX
STREET_W = range(60, 67, 2)
DEFAULTS['region.street_w'] = STREET_W
ALLEY_W = range(16, 21, 2)
DEFAULTS['region.alley_w'] = ALLEY_W
LOT_D = range(130, 151, 5)
DEFAULTS['region.lot_d'] = LOT_D
BASE_LOT = FT(50)
DEFAULTS['region.base_lot'] = BASE_LOT
BASE_TAKE = [(2, 70), (3, 30)]
DEFAULTS['region.base_take'] = BASE_TAKE
TRAILER_RUN_PCT = 20
DEFAULTS['region.trailer_run_pct'] = TRAILER_RUN_PCT
TRAILER_RUN = (2, 4)
DEFAULTS['region.trailer_run'] = TRAILER_RUN
PARK_PCT = 60
DEFAULTS['region.park_pct'] = PARK_PCT
PARK_PADS = (15, 40)
DEFAULTS['region.park_pads'] = PARK_PADS
PARK_PITCH = [26, 28, 30]
DEFAULTS['region.park_pitch'] = PARK_PITCH
PARK_LANE = [20, 22, 24]
DEFAULTS['region.park_lane'] = PARK_LANE
PARK_SITE_D = 90
DEFAULTS['region.park_site_d'] = PARK_SITE_D
PARK_FRONT = 40
DEFAULTS['region.park_front'] = PARK_FRONT
PARK_WB = 30
DEFAULTS['region.park_wb'] = PARK_WB
INDUSTRY = (FT(200), FT(300))
DEFAULTS['region.industry'] = INDUSTRY
RAIL_PCT = 60
DEFAULTS['region.rail_pct'] = RAIL_PCT
RAIL_GAP = FT(40)
DEFAULTS['region.rail_gap'] = RAIL_GAP
RAIL_PARALLEL = [1500, 2500, 3500]
DEFAULTS['region.rail_parallel'] = RAIL_PARALLEL
RAILTOWN_PCT = 50
DEFAULTS['region.railtown_pct'] = RAILTOWN_PCT
NORAIL_TOWN_PCT = 15
DEFAULTS['region.norail_town_pct'] = NORAIL_TOWN_PCT
RAIL_TOWN_GRIDS = [(2, 2), (2, 3), (2, 4), (3, 3)]
DEFAULTS['region.rail_town_grids'] = RAIL_TOWN_GRIDS
TOWN_SIZE = [("tiny", 60), ("mid", 30), ("big", 10)]
DEFAULTS['region.town_size'] = TOWN_SIZE
GRIDS = {"tiny": (TOWN_GRIDS, RAIL_TOWN_GRIDS),
         "mid": ([(6, 5), (7, 5), (7, 6), (8, 6)], [(5, 7), (6, 7), (5, 8), (6, 8)]),
         "big": ([(10, 9), (11, 9), (11, 10), (12, 10)], [(10, 8), (11, 8), (12, 8)])}
DEFAULTS['region.grids'] = GRIDS
RAGGED_PCT = 35
DEFAULTS['region.ragged_pct'] = RAGGED_PCT
RIBBON_DECAY = 20
DEFAULTS['region.ribbon_decay'] = RIBBON_DECAY
EDGE_HALF_PCT = 40
DEFAULTS['region.edge_half_pct'] = EDGE_HALF_PCT
FRINGE_PCT = 30
DEFAULTS['region.fringe_pct'] = FRINGE_PCT
MAIN_FACES = 6
DEFAULTS['region.main_faces'] = MAIN_FACES
TOWN_SB = {"front": (20, 30), "side": (5, 8), "rear": (20, 25), "corner": (10, 15)}
DEFAULTS['region.town_sb'] = TOWN_SB
RAIL_ROW = FT(100)
DEFAULTS['region.rail_row'] = RAIL_ROW
RAIL_ANGLE = (62, 80)
DEFAULTS['region.rail_angle'] = RAIL_ANGLE
ELEV_AC = (3, 6)
DEFAULTS['region.elev_ac'] = ELEV_AC
CEM = (FT(270), FT(275))
DEFAULTS['region.cem'] = CEM
CHURCH_AC = (1, 3)
DEFAULTS['region.church_ac'] = CHURCH_AC
FUEL = (FT(150), FT(150))
DEFAULTS['region.fuel'] = FUEL
CREEK_N = [(0, 30), (1, 50), (2, 20)]
DEFAULTS['region.creek_n'] = CREEK_N
CREEK_W = (20, 60)
DEFAULTS['region.creek_w'] = CREEK_W
CREEK_BUF = (150, 300)
DEFAULTS['region.creek_buf'] = CREEK_BUF
COOP_OFFICE = (FT(30), FT(50))
DEFAULTS['region.coop_office'] = COOP_OFFICE
PEOPLE_PER_HOUSE = 2.4
DEFAULTS['region.people_per_house'] = PEOPLE_PER_HOUSE
BANDS = (250, 750)
DEFAULTS['region.bands'] = BANDS
BIZ_P = {  # % chance per town by band (CRG); grocery floored at 30 in 250+ bands per user
    "post office": (82, 96, 96), "bank": (25, 67, 91), "grocery": (6, 30, 70), "cafe": (22, 54, 76), "tavern": (18, 37, 60),
    "gas station": (17, 60, 94), "elevator": (43, 55, 67), "farm supply": (10, 20, 30), "implement dealer": (7, 24, 43),
    "lumberyard": (7, 16, 51), "hardware": (3, 9, 27), "auto repair": (7, 26, 53), "beauty shop": (3, 9, 31),
    "insurance": (7, 33, 70), "church": (41, 78, 92), "locker plant": (3, 10, 12), "drug store": (0, 4, 29),
    "funeral home": (0, 16, 44), "nursing home": (0, 9, 47), "vet": (3, 12, 37), "city hall / fire": (50, 80, 100), "school": (0, 30, 70)}
DEFAULTS['region.biz_p'] = BIZ_P
CHURCH2_P = (0, 25, 50)
DEFAULTS['region.church2_p'] = CHURCH2_P
VACANT_PCT = 15
DEFAULTS['region.vacant_pct'] = VACANT_PCT
FADING = ["grocery", "hardware", "drug store", "variety"]
DEFAULTS['region.fading'] = FADING
STOREFRONT = ["cafe", "tavern", "insurance", "beauty shop", "drug store", "hardware", "locker plant", "grocery"]
DEFAULTS['region.storefront'] = STOREFRONT
EDGE = ["implement dealer", "auto repair", "vet", "nursing home", "school", "lumberyard", "farm supply"]
DEFAULTS['region.edge'] = EDGE
EDGE_FP = {"implement dealer": (330, 400, 120, 60, 80), "auto repair": (100, 150, 60, 40, 40), "vet": (200, 220, 60, 40, 50),
           "nursing home": (300, 400, 200, 120, 70), "school": (500, 600, 250, 150, 80), "lumberyard": (250, 300, 150, 60, 60),
           "farm supply": (150, 200, 80, 60, 50), "grocery (highway)": (200, 200, 100, 60, 70)}
DEFAULTS['region.edge_fp'] = EDGE_FP
COMM_COUNT = None
DEFAULTS['region.comm_count'] = COMM_COUNT
YEARS = [1985, 1990, 1995, 2000]
DEFAULTS['region.years'] = YEARS
BASE_YEAR = 1998
DEFAULTS['region.base_year'] = BASE_YEAR
FADE_1985 = 1.3
DEFAULTS['region.fade_1985'] = FADE_1985
FADING_TYPES = ["grocery", "hardware", "drug store", "variety", "implement dealer", "lumberyard", "locker plant", "cafe"]
DEFAULTS['region.fading_types'] = FADING_TYPES
BRANCH_PCT = (10, 50)
DEFAULTS['region.branch_pct'] = BRANCH_PCT
APPROVED = ["U1", "U2", "U3", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "16", "17",
            "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "30", "31", "32", "34", "35"]
DEFAULTS['validate.approved'] = APPROVED
WARN_ONLY = ["13", "14", "15", "18", "29", "33"]
DEFAULTS['validate.warn_only'] = WARN_ONLY
ALL = APPROVED + WARN_ONLY
DEFAULTS['validate.all'] = ALL
CATALOG = {'spawner.t': {'expression': '16',
               'note': 'ticks per inch',
               'source': 'user decision',
               'units': 'reference units (geometry in 1/16-inch ticks)',
               'min': None,
               'max': None,
               'step': None},
 'spawner.sf': {'expression': 'FT(1) ** 2',
                'note': 'ticks^2 per square foot',
                'source': 'user decision',
                'units': 'reference units (geometry in 1/16-inch ticks)',
                'min': None,
                'max': None,
                'step': None},
 'spawner.clr': {'expression': 'IN(4.5)',
                 'note': 'rule 11 corner clearance each side of a door RO',
                 'source': 'research',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'spawner.head': {'expression': 'IN(83)',
                  'note': 'rule 28 common head height (AH73 6\'-10"..6\'-11")',
                  'source': 'research',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'spawner.grid': {'expression': 'FT(2), FT(4)',
                  'note': 'rule 20',
                  'source': 'research',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'spawner.module': {'expression': 'FT(2), FT(4)',
                    'note': 'rule 20',
                    'source': 'research',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'spawner.habitable': {'expression': '{"living", "kitchen", "dining", "family", "bedroom"}',
                       'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                               'established.',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'spawner.public': {'expression': '{"living", "kitchen", "dining", "family"}',
                    'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                            'established.',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'spawner.style': {'expression': '{1985: (47, 39, 13), 1990: (44, 45, 11), 1995: (44, 50, 6), '
                                 '2000: (45, 50, 5)}',
                   'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                   'source': 'placeholder',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'spawner.beds': {'expression': '{1985: (24, 55, 20), 1990: (15, 57, 27), 1995: (17, 54, 28), '
                                '2000: (17, 53, 31)}',
                  'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                  'source': 'placeholder',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'spawner.baths': {'expression': '{1990: (18, 82), 1995: (18, 82), 2000: (11, 89)}',
                   'note': '1-1/2 or less vs 2+',
                   'source': 'user decision',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'spawner.found': {'expression': '{1985: (77, 11, 13), 1990: (81, 8, 11), 1995: (79, 10, 11), '
                                 '2000: (79, 13, 8)}',
                   'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                   'source': 'placeholder',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'spawner.garage': {'expression': '{1985: (13, 13, 73), 1990: (9, 8, 83), 1995: (9, 6, 84), 2000: '
                                  '(7, 4, 88)}',
                    'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                            'established.',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'spawner.outdoor': {'expression': '{1995: (42, 38, 27), 2000: (47, 36, 31)}',
                     'note': 'porch, deck, patio (series starts 1992)',
                     'source': 'user decision',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'spawner.garage_chain': {'expression': '{"double": ["double", "single", "detached"], "single": '
                                        '["single", "detached"], "none": ["none"], "detached": '
                                        '["detached"]}',
                          'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                                  'established.',
                          'source': 'placeholder',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'spawner.gw': {'expression': '{"double": FT(22), "single": FT(12), "none": 0, "detached": 0}',
                'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                'source': 'placeholder',
                'units': 'reference units (geometry in 1/16-inch ticks)',
                'min': None,
                'max': None,
                'step': None},
 'spawner.trailer_roof': {'expression': '["low-slope metal", "low-slope membrane", "shingled '
                                        'gable"]',
                          'note': 'PLACEHOLDER',
                          'source': 'placeholder',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'spawner.trailer_siding': {'expression': '["metal lap", "vinyl lap", "T1-11 plywood", "aluminum '
                                          'skirt+lap"]',
                            'note': 'PLACEHOLDER',
                            'source': 'placeholder',
                            'units': 'reference units (geometry in 1/16-inch ticks)',
                            'min': None,
                            'max': None,
                            'step': None},
 'spawner.trailer_w': {'expression': '[14, 16]',
                       'note': 'PLACEHOLDER ft (single-wide)',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'spawner.trailer_l': {'expression': '(60, 76)',
                       'note': 'PLACEHOLDER ft overall length',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'spawner.trailer_na': {'expression': '{"1": "door on the long side facing the pad, not the road '
                                      'end",\n'
                                      '              "4": "no garage expected", "5": "no garage '
                                      'expected", "8": "no dining room or garage",\n'
                                      '              "20": "factory-built; 14/16 ft widths are off '
                                      'the 4 ft module", "23": "single-wide area is about '
                                      '840-1,200 sf",\n'
                                      '              "24": "set on piers/skirting, no foundation '
                                      'draw", "25": "furnace/water heater closet, no utility '
                                      'room",\n'
                                      '              "30": "no porch/deck drawn", "21": "style = '
                                      'single-wide"}',
                        'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                                'established.',
                        'source': 'placeholder',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'spawner.side_pct': {'expression': '10, 5',
                      'note': '% of lot width, each side',
                      'source': 'user decision',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'spawner.side_min_pct': {'expression': '10, 5',
                          'note': '% of lot width, each side',
                          'source': 'user decision',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'spawner.front_pct': {'expression': '20, 15',
                       'note': '% of lot depth (125 ft lot -> 25 ft front)',
                       'source': 'user decision',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'spawner.front_min_pct': {'expression': '20, 15',
                           'note': '% of lot depth (125 ft lot -> 25 ft front)',
                           'source': 'user decision',
                           'units': 'reference units (geometry in 1/16-inch ticks)',
                           'min': None,
                           'max': None,
                           'step': None},
 'spawner.rear_pct': {'expression': '25, 20',
                      'note': '% of lot depth',
                      'source': 'user decision',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'spawner.rear_min_pct': {'expression': '25, 20',
                          'note': '% of lot depth',
                          'source': 'user decision',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'spawner.corner_side_x': {'expression': '2',
                           'note': 'street-side yard on a corner lot = 2x the side setback',
                           'source': 'user decision',
                           'units': 'reference units (geometry in 1/16-inch ticks)',
                           'min': None,
                           'max': None,
                           'step': None},
 'spawner.porch_project_max_w': {'expression': 'FT(70)',
                                 'note': 'lots this wide or narrower may project a porch...',
                                 'source': 'user decision',
                                 'units': 'reference units (geometry in 1/16-inch ticks)',
                                 'min': None,
                                 'max': None,
                                 'step': None},
 'spawner.porch_project': {'expression': 'FT(6)',
                           'note': '...up to 6 ft into the front setback',
                           'source': 'user decision',
                           'units': 'reference units (geometry in 1/16-inch ticks)',
                           'min': None,
                           'max': None,
                           'step': None},
 'region.mile': {'expression': 'FT(5280)',
                 'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                 'source': 'placeholder',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'region.n_mi': {'expression': '3',
                 'note': 'region size in miles (keeps the run small)',
                 'source': 'user decision',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'region.row': {'expression': 'FT(66)',
                'note': 'CRG road right-of-way',
                'source': 'research',
                'units': 'reference units (geometry in 1/16-inch ticks)',
                'min': None,
                'max': None,
                'step': None},
 'region.surf': {'expression': '{"gravel": (22, 24), "paved": (22, 22)}',
                 'note': 'CRG surface width ranges, ft',
                 'source': 'research',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'region.paved_pct': {'expression': '(22, 25)',
                      'note': 'CRG paved share of grid lines; lettered county routes paved first',
                      'source': 'research',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.town_pct': {'expression': '35',
                     'note': 'PLACEHOLDER chance a blacktop crossing gets a town',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.clear': {'expression': '(150, 300)',
                  'note': 'CRG intersection exclusion for rural lots, ft',
                  'source': 'research',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.farm_per_mile': {'expression': '[(0, 15), (1, 50), (2, 35)]',
                          'note': 'CRG ~1.2 farmsteads per road mile (weights give 1.2)',
                          'source': 'research',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'region.acreage_pct': {'expression': '25',
                        'note': 'PLACEHOLDER non-farm acreage per road mile',
                        'source': 'placeholder',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'region.farm_w': {'expression': 'range(300, 501, 50), range(400, 801, 50), (3, 9)',
                   'note': 'CRG',
                   'source': 'research',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'region.farm_d': {'expression': 'range(300, 501, 50), range(400, 801, 50), (3, 9)',
                   'note': 'CRG',
                   'source': 'research',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'region.farm_ac': {'expression': 'range(300, 501, 50), range(400, 801, 50), (3, 9)',
                    'note': 'CRG',
                    'source': 'research',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.farm_back': {'expression': '(100, 300)',
                      'note': 'CRG house distance back from the ROW, ft',
                      'source': 'research',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.acre_w': {'expression': 'range(150, 301, 25), range(250, 601, 50), (1, 5)',
                   'note': 'CRG 1-5 ac; dims PLACEHOLDER',
                   'source': 'placeholder',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'region.acre_d': {'expression': 'range(150, 301, 25), range(250, 601, 50), (1, 5)',
                   'note': 'CRG 1-5 ac; dims PLACEHOLDER',
                   'source': 'placeholder',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'region.acre_ac': {'expression': 'range(150, 301, 25), range(250, 601, 50), (1, 5)',
                    'note': 'CRG 1-5 ac; dims PLACEHOLDER',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.acre_back': {'expression': '60',
                      'note': 'CRG house 60 ft from the ROW',
                      'source': 'research',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.grove_w': {'expression': 'FT(80)',
                    'note': 'PLACEHOLDER shelterbelt width (grove on N and W sides is CRG)',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.town_grids': {'expression': '[(2, 2), (3, 2), (2, 3), (3, 3)]',
                       'note': 'CRG 4-9 blocks',
                       'source': 'research',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.block_cx': {'expression': 'range(380, 446, 5)',
                     'note': 'CRG block centers 380-445 ft (street direction)',
                     'source': 'research',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.street_w': {'expression': 'range(60, 67, 2)',
                     'note': 'CRG town street ROW 60-66',
                     'source': 'research',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.alley_w': {'expression': 'range(16, 21, 2)',
                    'note': 'CRG alley 16-20',
                    'source': 'research',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.lot_d': {'expression': 'range(130, 151, 5)',
                  'note': 'CRG town lot depth 130-150',
                  'source': 'research',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.base_lot': {'expression': 'FT(50)',
                     'note': 'CRG base lot; a house takes 1-3',
                     'source': 'research',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.base_take': {'expression': '[(2, 70), (3, 30)]',
                      'note': 'user default: stick-built houses take 2-3 base lots (1 lot = 0%)',
                      'source': 'user decision',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.trailer_run_pct': {'expression': '20',
                            'note': 'PLACEHOLDER chance a block face has a trailer cluster (user '
                                    'will send examples)',
                            'source': 'placeholder',
                            'units': 'reference units (geometry in 1/16-inch ticks)',
                            'min': None,
                            'max': None,
                            'step': None},
 'region.trailer_run': {'expression': '(2, 4)',
                        'note': 'PLACEHOLDER adjacent single 50 ft lots in a cluster',
                        'source': 'placeholder',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'region.park_pct': {'expression': '60',
                     'note': 'PLACEHOLDER chance a town has a mobile home park',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.park_pads': {'expression': '(15, 40)',
                      'note': 'PLACEHOLDER pads per park',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.park_pitch': {'expression': '[26, 28, 30]',
                       'note': 'PLACEHOLDER ft pad-to-pad along the lane (trailers ~12-16 ft '
                               'apart)',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.park_lane': {'expression': '[20, 22, 24]',
                      'note': 'PLACEHOLDER ft private lane width',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.park_site_d': {'expression': '90',
                        'note': 'PLACEHOLDER ft site depth off the lane (trailer 60-76 ft + '
                                'set-in)',
                        'source': 'placeholder',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'region.park_front': {'expression': '40',
                       'note': 'PLACEHOLDER ft from public road ROW to first pad',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.park_wb': {'expression': '30',
                    'note': 'PLACEHOLDER ft windbreak strip',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.industry': {'expression': '(FT(200), FT(300))',
                     'note': 'PLACEHOLDER feed mill / scales parcel on the rail corridor',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.rail_pct': {'expression': '60',
                     'note': 'PLACEHOLDER chance a region has a rail line',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.rail_gap': {'expression': 'FT(40)',
                     'note': 'PLACEHOLDER gap between road ROW and rail corridor on a parallel run',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.rail_parallel': {'expression': '[1500, 2500, 3500]',
                          'note': 'PLACEHOLDER ft of parallel run each side of the town road',
                          'source': 'placeholder',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'region.railtown_pct': {'expression': '50',
                         'note': 'PLACEHOLDER chance of a town where the rail passes a blacktop '
                                 'crossing',
                         'source': 'placeholder',
                         'units': 'reference units (geometry in 1/16-inch ticks)',
                         'min': None,
                         'max': None,
                         'step': None},
 'region.norail_town_pct': {'expression': '15',
                            'note': 'PLACEHOLDER chance of a no-rail town at the blacktop crossing',
                            'source': 'placeholder',
                            'units': 'reference units (geometry in 1/16-inch ticks)',
                            'min': None,
                            'max': None,
                            'step': None},
 'region.rail_town_grids': {'expression': '[(2, 2), (2, 3), (2, 4), (3, 3)]',
                            'note': 'PLACEHOLDER (rows run along the rail)',
                            'source': 'placeholder',
                            'units': 'reference units (geometry in 1/16-inch ticks)',
                            'min': None,
                            'max': None,
                            'step': None},
 'region.town_size': {'expression': '[("tiny", 60), ("mid", 30), ("big", 10)]',
                      'note': 'PLACEHOLDER: tiny <250 ppl, mid ~105-300 houses, big (George) '
                              '~315-600',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.grids': {'expression': '{"tiny": (TOWN_GRIDS, RAIL_TOWN_GRIDS),\n'
                                '         "mid": ([(6, 5), (7, 5), (7, 6), (8, 6)], [(5, 7), (6, '
                                '7), (5, 8), (6, 8)]),\n'
                                '         "big": ([(10, 9), (11, 9), (11, 10), (12, 10)], [(10, '
                                '8), (11, 8), (12, 8)])}',
                  'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                  'source': 'placeholder',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.ragged_pct': {'expression': '35',
                       'note': 'PLACEHOLDER outer-ring blocks left as farmland in mid/big towns '
                               '(ragged edge, dead ends)',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.ribbon_decay': {'expression': '20',
                         'note': 'PLACEHOLDER % keep-chance lost per block away from both main '
                                 'roads',
                         'source': 'placeholder',
                         'units': 'reference units (geometry in 1/16-inch ticks)',
                         'min': None,
                         'max': None,
                         'step': None},
 'region.edge_half_pct': {'expression': '40',
                          'note': 'PLACEHOLDER outer half-blocks left off (uneven depth)',
                          'source': 'placeholder',
                          'units': 'reference units (geometry in 1/16-inch ticks)',
                          'min': None,
                          'max': None,
                          'step': None},
 'region.fringe_pct': {'expression': '30',
                       'note': 'PLACEHOLDER street segments ending in a dead-end stub',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.main_faces': {'expression': '6',
                       'note': 'main street business district ~6 block faces',
                       'source': 'user decision',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.town_sb': {'expression': '{"front": (20, 30), "side": (5, 8), "rear": (20, 25), "corner": '
                                  '(10, 15)}',
                    'note': 'CRG, ft',
                    'source': 'research',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.rail_row': {'expression': 'FT(100)',
                     'note': 'PLACEHOLDER rail corridor width',
                     'source': 'placeholder',
                     'units': 'reference units (geometry in 1/16-inch ticks)',
                     'min': None,
                     'max': None,
                     'step': None},
 'region.rail_angle': {'expression': '(62, 80)',
                       'note': 'PLACEHOLDER degrees from east (NNE like Struble)',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.elev_ac': {'expression': '(3, 6)',
                    'note': 'CRG elevator / co-op parcel',
                    'source': 'research',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.cem': {'expression': '(FT(270), FT(275))',
                'note': 'CRG ~1.7 ac cemetery outside town',
                'source': 'research',
                'units': 'reference units (geometry in 1/16-inch ticks)',
                'min': None,
                'max': None,
                'step': None},
 'region.church_ac': {'expression': '(1, 3)',
                      'note': 'CRG country church',
                      'source': 'research',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.fuel': {'expression': '(FT(150), FT(150))',
                 'note': 'PLACEHOLDER fuel station parcel on a blacktop corner',
                 'source': 'placeholder',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'region.creek_n': {'expression': '[(0, 30), (1, 50), (2, 20)]',
                    'note': 'PLACEHOLDER creeks per region',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.creek_w': {'expression': '(20, 60)',
                    'note': 'PLACEHOLDER channel width ft',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.creek_buf': {'expression': '(150, 300)',
                      'note': 'PLACEHOLDER floodplain buffer ft each side (no lots)',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.coop_office': {'expression': '(FT(30), FT(50))',
                        'note': 'CRG co-op office on the elevator lot',
                        'source': 'research',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'region.people_per_house': {'expression': '2.4',
                             'note': 'PLACEHOLDER population estimate',
                             'source': 'placeholder',
                             'units': 'reference units (geometry in 1/16-inch ticks)',
                             'min': None,
                             'max': None,
                             'step': None},
 'region.bands': {'expression': '(250, 750)',
                  'note': '<250 / 250-749 / 750-1,499 people',
                  'source': 'user decision',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.biz_p': {'expression': '{  # % chance per town by band (CRG); grocery floored at 30 in '
                                '250+ bands per user\n'
                                '    "post office": (82, 96, 96), "bank": (25, 67, 91), "grocery": '
                                '(6, 30, 70), "cafe": (22, 54, 76), "tavern": (18, 37, 60),\n'
                                '    "gas station": (17, 60, 94), "elevator": (43, 55, 67), "farm '
                                'supply": (10, 20, 30), "implement dealer": (7, 24, 43),\n'
                                '    "lumberyard": (7, 16, 51), "hardware": (3, 9, 27), "auto '
                                'repair": (7, 26, 53), "beauty shop": (3, 9, 31),\n'
                                '    "insurance": (7, 33, 70), "church": (41, 78, 92), "locker '
                                'plant": (3, 10, 12), "drug store": (0, 4, 29),\n'
                                '    "funeral home": (0, 16, 44), "nursing home": (0, 9, 47), '
                                '"vet": (3, 12, 37), "city hall / fire": (50, 80, 100), "school": '
                                '(0, 30, 70)}',
                  'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                  'source': 'placeholder',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.church2_p': {'expression': '(0, 25, 50)',
                      'note': 'PLACEHOLDER 2nd church',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.vacant_pct': {'expression': '15',
                       'note': 'PLACEHOLDER share of storefronts vacant',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.fading': {'expression': '["grocery", "hardware", "drug store", "variety"]',
                   'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                   'source': 'placeholder',
                   'units': 'reference units (geometry in 1/16-inch ticks)',
                   'min': None,
                   'max': None,
                   'step': None},
 'region.storefront': {'expression': '["cafe", "tavern", "insurance", "beauty shop", "drug store", '
                                     '"hardware", "locker plant", "grocery"]',
                       'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                               'established.',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.edge': {'expression': '["implement dealer", "auto repair", "vet", "nursing home", '
                               '"school", "lumberyard", "farm supply"]',
                 'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                 'source': 'placeholder',
                 'units': 'reference units (geometry in 1/16-inch ticks)',
                 'min': None,
                 'max': None,
                 'step': None},
 'region.edge_fp': {'expression': '{"implement dealer": (330, 400, 120, 60, 80), "auto repair": '
                                  '(100, 150, 60, 40, 40), "vet": (200, 220, 60, 40, 50),\n'
                                  '           "nursing home": (300, 400, 200, 120, 70), "school": '
                                  '(500, 600, 250, 150, 80), "lumberyard": (250, 300, 150, 60, '
                                  '60),\n'
                                  '           "farm supply": (150, 200, 80, 60, 50), "grocery '
                                  '(highway)": (200, 200, 100, 60, 70)}',
                    'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                            'established.',
                    'source': 'placeholder',
                    'units': 'reference units (geometry in 1/16-inch ticks)',
                    'min': None,
                    'max': None,
                    'step': None},
 'region.comm_count': {'expression': 'None',
                       'note': 'superseded by BIZ_P',
                       'source': 'user decision',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'region.years': {'expression': '[1985, 1990, 1995, 2000]',
                  'note': 'one snapshot year per world (existing per-region build year)',
                  'source': 'user decision',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None},
 'region.base_year': {'expression': '1998',
                      'note': 'CRG rates are the 1998 baseline',
                      'source': 'research',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.fade_1985': {'expression': '1.3',
                      'note': 'PLACEHOLDER: towns <500 lost ~40% of retail sales 1983-1996 (CRG) '
                              '-> ~1.3x rate at 1985',
                      'source': 'placeholder',
                      'units': 'reference units (geometry in 1/16-inch ticks)',
                      'min': None,
                      'max': None,
                      'step': None},
 'region.fading_types': {'expression': '["grocery", "hardware", "drug store", "variety", '
                                       '"implement dealer", "lumberyard", "locker plant", "cafe"]',
                         'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                                 'established.',
                         'source': 'placeholder',
                         'units': 'reference units (geometry in 1/16-inch ticks)',
                         'min': None,
                         'max': None,
                         'step': None},
 'region.branch_pct': {'expression': '(10, 50)',
                       'note': 'PLACEHOLDER % of banks that are branches in 1985 / 2000',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'validate.approved': {'expression': '["U1", "U2", "U3", "1", "2", "3", "4", "5", "6", "7", "8", '
                                     '"9", "10", "11", "12", "16", "17",\n'
                                     '            "19", "20", "21", "22", "23", "24", "25", "26", '
                                     '"27", "28", "30", "31", "32", "34", "35"]',
                       'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                               'established.',
                       'source': 'placeholder',
                       'units': 'reference units (geometry in 1/16-inch ticks)',
                       'min': None,
                       'max': None,
                       'step': None},
 'validate.warn_only': {'expression': '["13", "14", "15", "18", "29", "33"]',
                        'note': 'PLACEHOLDER: inherited reference constant; provenance not '
                                'established.',
                        'source': 'placeholder',
                        'units': 'reference units (geometry in 1/16-inch ticks)',
                        'min': None,
                        'max': None,
                        'step': None},
 'validate.all': {'expression': 'APPROVED + WARN_ONLY',
                  'note': 'PLACEHOLDER: inherited reference constant; provenance not established.',
                  'source': 'placeholder',
                  'units': 'reference units (geometry in 1/16-inch ticks)',
                  'min': None,
                  'max': None,
                  'step': None}}

# Bounds not supplied by research are explicitly placeholders, not inferred research.
def _bound(value, high=False):
    if isinstance(value, bool): return True if high else False
    if isinstance(value, (int,float)): return max(value, value*4) if high else min(0,value)
    if isinstance(value, range): return value
    if isinstance(value, dict): return {k:_bound(v,high) for k,v in value.items()}
    if isinstance(value, (tuple,list)): return type(value)(_bound(v,high) for v in value)
    return value

for key, value in DEFAULTS.items():
    CATALOG[key].update(default=copy.deepcopy(value), min=_bound(value), max=_bound(value,True), step=1)
    CATALOG[key]['note'] += ' Range and step are PLACEHOLDER control limits; compound values retain reference structure.'

class Settings:
    def __init__(self, values=None):
        self.values = copy.deepcopy(DEFAULTS)
        if values:
            for key,value in values.items(): self.set(key,value)
    def set(self,key,value):
        if key not in DEFAULTS: raise KeyError(key)
        original=DEFAULTS[key]
        if isinstance(original,(int,float)):
            if isinstance(value,bool) or not isinstance(value,(int,float)): raise ValueError(key+' requires a number')
            if not __import__('math').isfinite(value): raise ValueError(key+' must be finite')
            value=max(CATALOG[key]['min'], min(CATALOG[key]['max'],value))
            if isinstance(original,int): value=int(value)
        elif type(value) is not type(original): raise ValueError(key+' requires '+type(original).__name__)
        self.values[key]=copy.deepcopy(value)
        return self

_LOCK=RLock()
@contextmanager
def applied(settings):
    # Reference functions share module globals. Serialize the scope and restore even on error.
    import spawner,region,validate
    modules={'spawner':spawner,'region':region,'validate':validate}
    with _LOCK:
        saved=[]
        try:
            for key,value in settings.values.items():
                mod,name=key.split('.'); name=name.upper()
                for target in modules.values():
                    if hasattr(target,name) and (target is modules[mod] or name in ('T','CLR','HEAD','GRID','MODULE','SF')):
                        saved.append((target,name,getattr(target,name))); setattr(target,name,copy.deepcopy(value))
            yield
        finally:
            for target,name,value in reversed(saved): setattr(target,name,value)

def configurable(fn):
    from functools import wraps
    @wraps(fn)
    def wrapped(*args,settings=None,**kwargs):
        if settings is None: return fn(*args,**kwargs)
        with applied(settings): return fn(*args,**kwargs)
    return wrapped
