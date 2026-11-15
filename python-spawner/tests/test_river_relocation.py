import copy

import world


def fixture_network(monkeypatch):
    mile=world.MILE
    monkeypatch.setattr(world,'_basin_angle',lambda seed:0)
    monkeypatch.setattr(world,'_basin_polygon',lambda *args:[(-10*mile,-10*mile),(10*mile,-10*mile),(10*mile,10*mile),(-10*mile,10*mile)])
    def reach(ident,points,order,downstream):
        return world.feature('rivers',ident,points=[(x*mile,y*mile) for x,y in points],
                             width=world.FT(30),widths=[world.FT(30)]*len(points),
                             stream_order=order,downstream=downstream,basin=(0,0))
    canonical=((0,0),(5*mile,0))
    monkeypatch.setattr(world,'_route_curve',lambda *args:('river:outlet:0:0',canonical))
    return [reach('up',[(0,1),(1,.2)],1,'down'),
            reach('down',[(1,.2),(2,0)],2,'river:outlet:0:0'),
            reach('river:outlet:0:0',[(0,0),(5,0)],3,'outside')]


def test_relocation_moves_all_incident_ends_without_changing_graph(monkeypatch):
    from river_relocation import relocate_nodes
    fs=fixture_network(monkeypatch)
    monkeypatch.setattr(world,'elevation',lambda seed,x,y:100-10*x/world.MILE+10*y/world.MILE)
    original=copy.deepcopy(fs)
    relocate_nodes(14,0,0,fs)
    assert fs[0]['points'][-1]==fs[1]['points'][0]
    assert fs[1]['points'][0]!=original[1]['points'][0]
    assert world.math.dist(fs[1]['points'][0],original[1]['points'][0])<=.5*world.MILE
    assert fs[2]['points']==original[2]['points']
    for before,after in zip(original,fs):
        for key in ('id','downstream','stream_order','width','widths'):
            assert after[key]==before[key]
        assert world.elevation(14,*after['points'][0])>world.elevation(14,*after['points'][-1])


def test_infeasible_elevation_move_is_retained_and_reported(monkeypatch):
    from river_relocation import relocate_nodes
    fs=fixture_network(monkeypatch)
    monkeypatch.setattr(world,'elevation',lambda seed,x,y:min(121,100+100*y/world.MILE))
    point=fs[1]['points'][0]
    relocate_nodes(14,0,0,fs)
    assert fs[1]['points'][0]==point
    report=fs[1]['_node_relocation']
    assert report['status']=='blocked'
    assert report['distance_mi']<.4
    assert report['rejected']['elevation']>0


def test_relocation_is_independent_of_feature_iteration_order(monkeypatch):
    from river_relocation import relocate_nodes
    fs=fixture_network(monkeypatch)
    monkeypatch.setattr(world,'elevation',lambda seed,x,y:100-10*x/world.MILE+10*y/world.MILE)
    reversed_fs=copy.deepcopy(list(reversed(fs)))
    relocate_nodes(52,0,0,fs)
    relocate_nodes(52,0,0,reversed_fs)
    assert {f['id']:f for f in fs}=={f['id']:f for f in reversed_fs}


def test_relocation_uses_canonical_outlet_geometry_not_mutable_cached_rendering(monkeypatch):
    from river_relocation import relocate_nodes
    fs=fixture_network(monkeypatch)
    monkeypatch.setattr(world,'elevation',lambda seed,x,y:100-10*x/world.MILE+10*y/world.MILE)
    changed=copy.deepcopy(fs)
    changed[-1]['points'].insert(1,(2.5*world.MILE,2*world.MILE))
    changed[-1]['bounds']=world.box(changed[-1]['points'],changed[-1]['width']/2)
    relocate_nodes(14,0,0,fs)
    relocate_nodes(14,0,0,changed)
    assert fs[1]['points']==changed[1]['points']
    assert fs[1]['_node_relocation']==changed[1]['_node_relocation']
