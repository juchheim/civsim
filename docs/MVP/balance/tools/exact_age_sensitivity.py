"""Expected-demography baseline under 09's ideal-food and ideal-health assumptions.

The model uses exogenous research/cap positions and C1 checkpoint seasons. It
simulates exact-age cohorts, pregnancy countdowns, mortality and fission without
fitting live population outputs. This is a controlled demographic baseline; it
does not establish whether the separate live population gate passes because it
omits live ecology, food shortfalls and stochastic mechanisms.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import balance_sim as B
import params as P

ROSTER = [(6,1),(9,0),(18,1),(20,0),(22,1),(24,0),(26,1),(28,0),(30,1),(32,0),(50,1),(55,0)] # age, female flag
CHECKPOINTS = [(0.999, 7.75), (1.999, 17.75), (2.999, 29.50)]
ARCH = {
 'Balanced': dict(fert=1.0, food=1.0, adultmort=1.0, leanadj=0.0),
 'Grower': dict(fert=1.30, food=1.11, adultmort=1.0, leanadj=0.0),
 'Builder': dict(fert=1.10, food=1.07, adultmort=1.0, leanadj=-0.02),
 'Wanderer': dict(fert=1.0, food=0.95, adultmort=1.05, leanadj=0.0),
}
# Live E0-E2 base annual hazards (excluding modeled ecology-specific excess).
CHILD = [.036,.033,.035]
ADULT = [.012,.011,.012]
ELDER45 = [.023,.021,.021]
WINTER = [2.0,1.7,1.4]
GOMPERTZ = 1.08872


def total(coh): return sum(v for bysex in coh for states in bysex for v in states)
def band_totals(coh):
    return [sum(coh[a][sx][pr] for a in range(lo,hi) for sx in range(2) for pr in range(4)) for lo,hi in [(0,14),(14,45),(45,100)]]
def hazard(age,e,adult_mult):
    if age==0: return CHILD[e]*2.333333333
    if age<5: return CHILD[e]*1.166666667
    if age<14: return CHILD[e]*0.777777778
    if age<45: return ADULT[e]*adult_mult
    return min(4.0, ELDER45[e]*(GOMPERTZ**(age-45)))
def init():
    c=[[[0.0]*4 for _ in range(2)] for _ in range(100)]
    for age,female in ROSTER: c[age][0 if female else 1][0]+=1
    return c

def scale_coh(coh, factor):
    return [[[v*factor for v in states] for states in bysex] for bysex in coh]

def run(archetype):
    profile=ARCH[archetype]
    s=P.scenario('tuned','Standard',archetype)
    # Exact live calibration, replacing canonical archetype defaults.
    s['arch']['Fertility']=profile['fert']; s['arch']['FoodCap']=profile['food']
    s['arch']['AdultMort']=profile['adultmort']; s['arch']['LeanShareAdjustment']=profile['leanadj']
    # External research/milestone schedule: KEY-1/2/3 at 4650/10650/17700 ticks.
    # Progress is interpolated between the fixed C1 checkpoints; population never
    # drives research in this comparator.
    last_t=118
    settlements=[dict(coh=init(), last_fission=-4, pending=False)]
    outputs={}; first_fission=None; births_cum=deaths_cum=0.0
    period_births=0.0; period_deaths=[0.0,0.0,0.0]; period_person_seasons=[0.0,0.0,0.0]
    for season in range(1,last_t+1):
        if season<=31: e=0; pos=.999*season/31
        elif season<=71: e=1; pos=1.0+.999*(season-31)/40
        else: e=2; pos=2.0+.999*(season-71)/47
        cp=B.caps(s,e,pos)
        foodcap=cp['food']; housing=cp['housing']; groupcap=cp['group']
        cap=min(foodcap,housing); fissioncap=min(foodcap,housing,groupcap)
        # B.caps already includes the live Builder LeanShareAdjustment input.
        lean_share=cp['lean']/max(1e-12,foodcap)
        smax=max(1,B.era_derived(s)[e]['smax'])
        # Resolve last season's fissions before this season's mortality, as live does.
        new_settlements=[]
        for st in settlements:
            if st['pending']:
                if (season-1-st['last_fission']>=4 and len(settlements)<smax and
                    total(st['coh'])*.4>=6):
                    st['coh']=scale_coh(st['coh'],.6)
                    st['last_fission']=season-1; st['pending']=False
                    new_settlements.append(dict(coh=scale_coh(st['coh'],2/3),
                                                last_fission=season-1,pending=False))
                    # Parent is 60%; child is 40% of the pre-split cohort.
                    # 2/3 × parent's post-split 60% equals 40% of original.
                    if first_fission is None: first_fission=(season-1)/4
                else:
                    st['pending']=False
        settlements.extend(new_settlements)
        prior_total=sum(total(st['coh']) for st in settlements)
        # Person-time is measured at season start, matching the harness.
        for age in range(100):
            band=0 if age<14 else 1 if age<45 else 2
            period_person_seasons[band]+=sum(st['coh'][age][sx][pr] for st in settlements for sx in range(2) for pr in range(4))
        deaths=births=0.0; deaths_by_band=[0.0,0.0,0.0]; winter=(season%4==0); wm=WINTER[e] if winter else 1.0
        fertility=.13*P.ERA['FertMult']['tuned'][e]*profile['fert']
        pconceive=1.0/(1.0/fertility-3.0)
        for st in settlements:
            coh=st['coh']; start=total(coh); deaths_here=0.0
            # Expected mortality from live BaseHazard age bands and winter multiplier.
            for age in range(100):
                for sx in range(2):
                    for pr in range(4):
                        annual=hazard(age,e,profile['adultmort'])
                        if 14<=age<45:
                            annual*=.94
                            if sx==0 and pr==1:
                                delivery_rate=.5*.13*P.ERA['FertMult']['tuned'][e]*profile['fert']
                                annual+=ADULT[e]*profile['adultmort']*.06/delivery_rate
                        pdeath=min(1.0,annual*wm/4.0)
                        old=coh[age][sx][pr]; lost=old*pdeath
                        coh[age][sx][pr]=old-lost; deaths_here+=lost
                        deaths_by_band[0 if age<14 else 1 if age<45 else 2]+=lost
            after_survival=total(coh)
            assert abs(after_survival-(start-deaths_here))<1e-8,(archetype,season,'mortality mass')
            deaths+=deaths_here
            # Pregnancy countdown; a delivering mother becomes eligible next season,
            # not in the same season (matches the live if/else branch).
            births_here=0.0
            crowd=min(1.0,(start/max(1e-12,cap))**6)
            for age in range(100):
                female=coh[age][0]; eligible=female[0]
                for pr in (1,2,3):
                    n=female[pr]; female[pr]=0.0
                    if pr==1: births_here+=n; female[0]+=n
                    else: female[pr-1]+=n
                if 14<=age<45:
                    conceive=eligible*pconceive*(1-crowd)
                    female[0]-=conceive; female[3]+=conceive
            assert abs(total(coh)-after_survival)<1e-8,(archetype,season,'pregnancy mass')
            # Winter aging shifts only people already present at season start;
            # births in this Winter remain age 0.
            if winter:
                aged=[[[0.0]*4 for _ in range(2)] for _ in range(100)]
                for age in range(100):
                    dest=min(99,age+1)
                    for sx in range(2):
                        for pr in range(4): aged[dest][sx][pr]+=coh[age][sx][pr]
                coh=aged; st['coh']=coh
            after_aging=total(coh)
            assert abs(after_aging-after_survival)<1e-8,(archetype,season,'aging mass')
            coh[0][0][0]+=births_here*.5; coh[0][1][0]+=births_here*.5
            births+=births_here
            assert abs(total(coh)-(start-deaths_here+births_here))<1e-8,(archetype,season,'cohort conservation')
            # Live triggers at season end; pending fission resolves next boundary.
            pop=total(coh)
            cap_pressure=pop>=.85*fissioncap
            late_pressure=winter and pop>=foodcap*lean_share
            if (season-1-st['last_fission']>=4 and len(settlements)<smax and
                (cap_pressure or late_pressure) and pop*.4>=6):
                st['pending']=True
        births_cum+=births; deaths_cum+=deaths; period_births+=births
        for b in range(3): period_deaths[b]+=deaths_by_band[b]
        current_total=sum(total(st['coh']) for st in settlements)
        assert abs(current_total-(prior_total-deaths+births))<1e-8,(archetype,season,'global conservation')
        assert len(settlements)<=smax,(archetype,season,'settlement cap')
        if any(v < -1e-12 for st in settlements for bysex in st['coh'] for states in bysex for v in states):
            raise AssertionError((archetype,season,'negative cohort'))
        for target_pos,target_year in CHECKPOINTS:
            if season==round(target_year*4):
                bands=[sum(st['coh'][age][sx][pr] for st in settlements
                           for age in range(lo,hi) for sx in range(2) for pr in range(4))
                       for lo,hi in [(0,14),(14,45),(45,100)]]
                outputs[target_pos]=dict(pop=current_total,child=bands[0],adult=bands[1],elder=bands[2],
                    S=len(settlements),research_era=e,research_pos=pos,year=season/4,
                    births=births_cum,deaths=deaths_cum,period_births=period_births,
                    period_deaths=period_deaths.copy(),period_person_seasons=period_person_seasons.copy())
                period_births=0.0; period_deaths=[0.0,0.0,0.0]; period_person_seasons=[0.0,0.0,0.0]
    return outputs,first_fission

if __name__=='__main__':
    print('Exact-age expected-demography baseline: ideal food/health; exogenous C1 research positions; no live ecological or stochastic effects.')
    for arch in ARCH:
        out,ff=run(arch); print(f'\n{arch} first_fission_year={ff}')
        for pos,_ in CHECKPOINTS:
            x=out[pos]; print(f"E{int(pos)} year={x['year']:.2f} simera={x['research_era']} pos={x['research_pos']:.3f} pop={x['pop']:.2f} child={x['child']:.2f} adult={x['adult']:.2f} elder={x['elder']:.2f} settlements={x['S']}")
