"""Presentation metadata only; generator values and preset keys stay canonical."""
import re

LABELS={
 'spawner.t':'Geometry precision', 'spawner.sf':'Area conversion',
 'spawner.clr':'Door corner clearance','spawner.head':'Door and window head height',
 'spawner.grid':'Room layout grid','spawner.module':'House layout module',
 'spawner.habitable':'Habitable room types','spawner.public':'Public room types',
 'spawner.style':'House style weights by year','spawner.beds':'Bedroom count weights by year',
 'spawner.baths':'Bathroom count weights by year','spawner.found':'Foundation type weights by year',
 'spawner.garage':'Garage type weights by year','spawner.outdoor':'Porch, deck and patio weights by year',
 'spawner.garage_chain':'Garage fallback order','spawner.gw':'Garage widths',
 'spawner.trailer_na':'Manufactured home rule exceptions','spawner.corner_side_x':'Corner lot side setback multiplier',
 'spawner.porch_project_max_w':'Maximum lot width allowing porch projection',
 'spawner.porch_project':'Allowed porch projection into setback',
 'region.mile':'World distance conversion','region.n_mi':'Reference region width',
 'region.row':'Road right-of-way width','region.surf':'Road surface width range',
 'region.clear':'Rural lot clearance from intersections','region.farm_per_mile':'Farm count weights per road mile',
 'region.acreage_pct':'Rural acreage probability','region.acre_w':'Acreage lot width',
 'region.acre_d':'Acreage lot depth','region.acre_ac':'Acreage parcel area',
 'region.acre_back':'Acreage house setback','region.farm_w':'Farm lot width',
 'region.farm_d':'Farm lot depth','region.farm_ac':'Farm parcel area','region.farm_back':'Farm house setback',
 'region.grove_w':'Farm shelterbelt width','region.town_grids':'Town block layout choices',
 'region.block_cx':'Town block center spacing','region.street_w':'Town street right-of-way width',
 'region.alley_w':'Alley width','region.lot_d':'Town lot depth','region.base_lot':'Base town lot width',
 'region.base_take':'House lot count weights','region.rail_row':'Rail corridor width',
 'region.railtown_pct':'Town probability near rail','region.norail_town_pct':'Town probability without rail',
 'region.rail_town_grids':'Rail town block layout choices','region.town_sb':'Town building setbacks',
 'region.elev_ac':'Grain elevator parcel area','region.cem':'Cemetery dimensions',
 'region.fuel':'Fuel depot dimensions','region.creek_n':'Creek count weights',
 'region.creek_w':'Creek channel width','region.creek_buf':'Creek floodplain buffer',
 'region.coop_office':'Cooperative office dimensions','region.biz_p':'Business placement probability',
 'region.church2_p':'Second church probability','region.bands':'Town population bands',
 'region.comm_count':'Commercial building counts','region.edge_fp':'Highway business footprints',
 'region.fade_1985':'Decline weights since 1985','region.main_faces':'Main street block faces',
 'controls.debounce_ms':'Wait after a settings change','controls.port':'Local control page port',
 'controls.max_request_bytes':'Maximum preset request size','world.window_miles':'Starting view width',
 'world.tile_miles':'Settlement ownership tile width','world.chunk_miles':'Display chunk width',
 'world.pan_fraction':'Distance moved by pan buttons','world.zoom_factor':'Zoom step multiplier',
 'generation.parity':'Reference image parity mode',
}
LABELS.update({
 'region.paved_pct':'Share of reference roads that are paved',
 'region.town_pct':'Chance of a town at a paved-road crossing',
 'region.rail_pct':'Chance of rail in a reference region',
 'region.ragged_pct':'Share of towns with irregular street edges',
 'region.ribbon_decay':'Drop in growth along ribbon streets',
 'region.edge_half_pct':'Share of partially built town edges',
 'region.fringe_pct':'Share of lots on the town fringe',
 'region.trailer_run_pct':'Chance of a manufactured-home cluster',
 'region.trailer_run':'Homes per manufactured-home cluster',
 'region.industry':'Industrial parcel dimensions',
 'region.rail_gap':'Distance between parallel road and rail corridors',
 'region.rail_parallel':'Length of rail beside a road',
 'region.rail_angle':'Rail crossing angle range',
 'region.town_size':'Town size selection weights',
 'region.grids':'Block layouts by town size',
 'region.people_per_house':'Residents per house',
 'region.vacant_pct':'Share of vacant commercial buildings',
 'region.fading':'Commercial decline settings',
 'region.storefront':'Downtown storefront dimensions',
 'region.edge':'Highway business placement',
 'region.years':'Development years',
 'region.base_year':'Starting development year',
 'region.fading_types':'Business types affected by decline',
 'region.branch_pct':'Chance of branching town streets',
 'spawner.trailer_roof':'Manufactured-home roof choices',
 'spawner.trailer_siding':'Manufactured-home siding choices',
 'spawner.trailer_w':'Manufactured-home widths',
 'spawner.trailer_l':'Manufactured-home length range',
 'spawner.side_pct':'Side yard setback as share of lot width',
 'spawner.side_min_pct':'Minimum side yard share of lot width',
 'spawner.front_pct':'Front yard setback as share of lot depth',
 'spawner.front_min_pct':'Minimum front yard share of lot depth',
 'spawner.rear_pct':'Rear yard setback as share of lot depth',
 'spawner.rear_min_pct':'Minimum rear yard share of lot depth',
 'validate.approved':'Approved house validation rules',
 'validate.warn_only':'House rules reported as warnings',
 'validate.all':'All house validation rules',
 'generation.snapshot_year':'Year shown on the map',
 'generation.max_feature_attempts':'Placement attempts per map feature',
 'commercial.downtown_share':'Share of businesses placed downtown',
 'stages.rail':'Show rail corridors', 'stages.creeks':'Reserve local creek floodplains',
 'stages.towns':'Generate towns', 'stages.businesses':'Generate businesses',
 'stages.trailers':'Generate manufactured homes', 'stages.houses':'Generate house plans',
 'stages.trees':'Show tree trunk markers',
 'controls.seed':'World generation seed', 'trees.spacing_ft':'Distance between tree markers',
 'world.main_spacing_miles':'Distance between paved main roads',
 'world.level_b_pct':'Share of section roads with dirt surfaces',
 'world.missing_pct':'Share of road segments omitted',
 'world.rail_spacing_miles':'Distance between rail corridors',
 'world.rail_enabled_pct':'Share of rail corridors in use',
 'world.river_spacing_miles':'Distance between river valleys',
 'world.tributary_spacing_miles':'Distance between tributary junctions',
 'world.river_width_ft':'Width of main rivers',
 'world.relief_ft':'Height of watershed ridges above valleys',
 'world.contour_interval_ft':'Elevation between contour lines',
 'world.contour_cell_ft':'Ground spacing of terrain samples',
 'world.halo_tiles':'Settlement tiles examined outside a detailed view',
 'world.tree_radius_ft':'Radius of tree trunk markers',
 'world.hill_wavelength_miles':'Reference hill wavelength',
 'world.river_wander_miles':'Sideways meander distance of rivers',
 'world.river_wave_steps':'Tributary intervals per river meander',
 'world.tile_cache_size':'Settlement tiles retained in memory',
 'world.rail_draw_ft':'Width of rail strokes on the map',
 'world.south_drop_ft':'Terrain elevation drop toward the south',
 'verify.minimum_angle_deg':'Smallest permitted rail crossing angle',
 'verify.square_deviation_deg':'Allowed deviation from a square crossing',
 'verify.square_share':'Required share of near-square rail crossings',
 'verify.bin_ft':'Ground spacing of collision index cells',
 'rural.outbuildings_min':'Minimum rural outbuilding count',
 'rural.outbuildings_max':'Maximum rural outbuilding count',
 'rural.building_gap_ft':'Space between rural outbuildings',
 'rural.lane_width_ft':'Width of farm and acreage lanes',
 'rural.bent_lanes_pct':'Share of rural lanes with bends',
 'rural.grove_density':'Share of shelterbelt positions with trees',
 'rural.lot_corner_fraction':'Maximum rural lot corner cut',
 'world.rail_diagonal_pct':'Share of rural rail sections with diagonal bends',
 'world.rail_bend_miles':'Sideways distance of rail bends',
})
WORDS={'pct':'probability','mi':'miles','w':'width','d':'depth','ac':'acres','sb':'setbacks','biz':'business','fp':'footprints','n':'count','ms':'milliseconds','ft':'feet','min':'minimum','max':'maximum','clr':'clearance','gw':'garage widths','found':'foundation','sf':'square feet','na':'exceptions'}
TICK_LENGTHS=set('spawner.clr spawner.head spawner.grid spawner.module spawner.gw spawner.porch_project_max_w spawner.porch_project region.row region.grove_w region.base_lot region.industry region.rail_gap region.rail_row region.cem region.fuel region.coop_office'.split())
FEET=set('spawner.trailer_w spawner.trailer_l region.surf region.clear region.farm_back region.acre_back region.rail_parallel region.town_sb region.creek_w region.creek_buf'.split())

def words(name):
 return ' '.join(WORDS.get(w,w) for w in re.sub(r'\d+_\d+$','',name).strip('_').split('_')).capitalize()

def presentation(key,spec):
 name=key.split('.',1)[-1]
 advanced='numbers' in name or key in ('spawner.t','spawner.sf','region.mile','region.n_mi')
 if advanced: group='Advanced reference rules'
 elif key.startswith('stages.'): group='Generation stages'
 elif key.startswith('world.'): group='Endless world and navigation'
 elif key.startswith('controls.'): group='Page behavior'
 elif key.startswith('verify.') or key.startswith('validate.'): group='Validation and repair'
 elif key.startswith('spawner.') or key.startswith('generation.'): group='Houses and lots'
 elif key.startswith('rural.'): group='Towns, roads and countryside'
 elif 'tree' in key: group='Trees'
 elif any(t in key for t in ('rail','creek')): group='Rail and waterways'
 else: group='Towns, roads and countryside'
 units=spec['units']; scale=1
 if isinstance(spec['default'],bool): units='on/off'
 if key.endswith('_pct'): units='%'
 if key in TICK_LENGTHS: units='ft';scale=192
 elif key in FEET: units='ft'
 elif key.endswith('_ac'): units='acres'
 elif 'weights' in LABELS.get(key,''): units='relative weights'
 elif key in ('region.farm_ac','region.acre_ac','region.elev_ac','region.church_ac'): units='acres'
 elif key in ('region.paved_pct',): units='%'
 elif units.startswith('reference units'): units='reference values; see help'
 label=LABELS.get(key,words(name))
 note=spec['note'].split(' Range and step')[0].split(' Control bounds')[0]
 if key=='world.window_miles': note='Initial visible width. Pan anywhere; zoom buttons and wheel change the current view.'
 tooltip=f'{label} ({units}): '+note.split('. ')[0]
 help=f'{label}. {note} Units: {units}. Source: {spec["source"]}. Preset key: {key}.'
 return dict(label=label,group=group,display_units=units,display_scale=scale,tooltip=tooltip,help=help)
