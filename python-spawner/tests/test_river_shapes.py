def test_shared_phase_group_does_not_copy_another_reach():
    from world import _edge_curve, MILE, FT
    a=_edge_curve(14,'reach-a',(0,0),(MILE,0),FT(14),('receiver',1,0))
    b=_edge_curve(14,'reach-b',(0,0),(MILE,0),FT(14),('receiver',1,0))
    assert a!=b


def test_window_bearings_ignore_segments_outside_the_window(monkeypatch):
    import world
    from verification import river_geometry_metrics
    m=world.MILE
    reach=world.feature('rivers','outside-turn',points=[(-2*m,0),(2*m,0),(2*m,2*m),(-2*m,2*m)],
                        width=world.FT(10),widths=[world.FT(10)]*4,stream_order=1,
                        downstream='outside',source=True,basin=(0,0))
    monkeypatch.setattr(world,'rivers',lambda seed,rect:iter([reach]))
    monkeypatch.setattr(world,'basin',lambda *args:[reach])
    metrics=river_geometry_metrics(14,(-.5*m,-.5*m,m,m))
    assert metrics['bearing_entropy']==0
    assert metrics['ewest_fraction']==1


def test_finished_crossing_scan_distinguishes_crossing_and_confluence():
    import world
    from river_geometry import finished_crossings
    m=world.MILE
    def reach(ident,points,downstream='outside'):
        return world.feature('rivers',ident,points=points,width=world.FT(10),downstream=downstream)
    a=reach('a',[(0,0),(m,m)])
    b=reach('b',[(0,m),(m,0)])
    assert finished_crossings(14,[a,b])
    a=reach('a',[(m/2,-m),(m/2,0)],'b')
    b=reach('b',[(0,0),(m,0)])
    assert not finished_crossings(14,[a,b])


def test_repinning_the_same_approach_is_idempotent():
    import copy
    import math
    import world
    points=world._edge_curve(14,'pin-stability',(0,0),(1.2*world.MILE,0),world.FT(12))
    f=world.feature('rivers','pin-stability',points=points,width=world.FT(12),
                    widths=[world.FT(12)]*len(points),stream_order=1,_junction_seed=14)
    tangent=(math.cos(math.pi/3),math.sin(math.pi/3))
    world._aim_end(f,tangent,True,.3*world.MILE)
    before=copy.deepcopy(f)
    world._aim_end(f,tangent,True,.3*world.MILE)
    assert len(f['points'])==len(before['points'])
    assert max(math.dist(a,b) for a,b in zip(f['points'],before['points']))<1e-6
    assert f['widths']==before['widths']


def test_equal_junction_samples_do_not_make_overlapping_approach_stubs(monkeypatch):
    import math
    import world
    m=world.MILE
    def reach(ident,points,order,downstream):
        return world.feature('rivers',ident,points=[(x*m,y*m) for x,y in points],
                             width=world.FT(15),widths=[world.FT(15)]*len(points),
                             stream_order=order,downstream=downstream)
    a=reach('a',[(-1,-.5),(-.5,-.25),(0,0)],1,'r')
    b=reach('b',[(-1,-1),(-.5,-.5),(0,0)],1,'r')
    r=reach('r',[(0,0),(.75,0),(1.5,0)],2,'outside')
    original=world.H
    monkeypatch.setattr(world,'H',lambda *args:0 if args[1]=='junction-normal' else original(*args))
    world._angle_reaches(14,[a,b,r])
    aa=math.atan2(a['_junction_direction'][1],a['_junction_direction'][0])
    bb=math.atan2(b['_junction_direction'][1],b['_junction_direction'][0])
    assert abs(aa-bb)>=math.radians(20)-1e-12


def test_continuation_rounding_does_not_reintroduce_a_self_crossing():
    import world
    ident='river:0:0:edge:9:2:part:1'
    a=(9894158.268651359,7379146.628178952)
    b=(9223504.35458478,7047406.2209005095)
    points=world._edge_curve(13,ident,a,b,world.FT(15),('river:0:0:edge:8:2',1,1),1)
    f=world.feature('rivers',ident,points=points,width=world.FT(15),
                    widths=[world.FT(15)]*len(points),stream_order=1,_junction_seed=13)
    for _ in range(4):
        world._aim_end(f,(-.9483164653159329,-.3173261439130016),True,.3*world.MILE)
    assert not world._curve_collision(f['points'],f['points'])
    world._aim_end(f,(-.8963363556842977,-.44337471452327126),False)
    assert not world._curve_collision(f['points'],f['points'])


def test_resampling_removes_zero_length_numerical_segments():
    import world
    f=world.feature('rivers','duplicate',points=[(0,0),(1,1),(2,0)],width=10,widths=[10,10,10])
    world._resample_widths(f,[(0,0),(1,1),(1+1e-10,1),(2,0)])
    assert len(f['points'])==3
    assert f['points'][0]==(0,0) and f['points'][-1]==(2,0)
