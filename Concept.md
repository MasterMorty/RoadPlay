# RoadPlay
AR-Assisted Traffic Rule Learning on a Physical Street Carpet

Project Concept - Wout, Serhat, Florian

---

## 1. The Idea

Street carpets are one of the most classic children's toys. Kids place toy cars on them and build little worlds in their head, but the cars don't do anything on their own. We asked: what if the cars actually knew the traffic rules? What if breaking a rule then also had a consequence you could see?

RoadPlay puts a Meta Quest headset above a physical street carpet. A child drives a real toy car on the carpet. The AR system tracks the car in real time and overlays virtual traffic, rule-following cars, traffic lights, and road signs, directly onto the carpet. When the child's car runs a red light, cuts off another car, or ignores a yield sign, the virtual cars react. They stop, honk, or nearly collide. The child learns traffic rules not by being told, but by causing consequences and seeing them play out in their physical space.

The core system has three parts: (1) spatial anchoring -> mapping the carpet roads so virtual elements stay locked to the physical world, (2) a traffic rule engine that governs the virtual cars and detects when the physical car breaks a rule, and (3) visual AR feedback that makes the consequences immediately readable to a child.

---

## 2. Research Question

Does AR-based consequence feedback during free play with a physical toy car improve children's intuitive understanding of basic traffic rules compared to unassisted play?

We are not running a formal user study :( sadly we don't have access to child participants or the time to set one up properly (for testing children). Instead, we will do a small informal pilot where we observe our own interaction with the prototype, document what works and what doesn't and reflect critically on whether the system creates the kind of feedback loop we hypothesise would support learning. This should be honest and scoped, and it lets us engage meaningfully with the research question, hopefully without overclaiming.

---

## 3. Connection to Course Papers

### Guided Reality

Guided Reality is an AR system that generates step-by-step visual guidance embedded in physical space using LLMs and vision models. It should be exactly what RoadPlay does with traffic rules. Instead of a manual telling a child what a yield sign means, the consequence plays out on the road they are already looking at. We borrow the principle of spatially grounded feedback but apply it to a playful, child-facing context.

### A Design Space for Vision Augmentations

This paper introduces a systematic design space for how AR can augment human perception through digital eyewear, covering dimensions like stimulus field, temporal presentation, and photo reception. We want to use this framework to position RoadPlay's design decisions: our augmentations are spatially anchored (tied to carpet geometry), temporally reactive (triggered by real-time rule violations), and extending rather than replacing natural vision. The design space should give us a principled vocabulary for describing and justifying our AR approach.

### CinemaWorld

CinemaWorld augments a viewer's physical environment in synchrony with a film. Virtual elements are spatially embedded and react to real-world context in real time. RoadPlay should apply the same principle: the physical carpet is the world, and the virtual layer reacts continuously to what the real car does. CinemaWorld demonstrates that this kind of tight physical-virtual coupling is technically feasible on the Quest and produces strong immersion, which should ground our design choice.

---

## 4. Individual Contributions

All three group members contribute to the shared concept, help prepare the in-class demo, and write sections of the final report. Each person also owns one distinct technical workstream that forms their individually assessable contribution:

| Person | Core technical contribution | Shared contribution |
|---|---|---|
| Wout | AR system & spatial anchoring -> Quest setup, marker-based car tracking, virtual overlay rendering in the physical carpet space | Concept, related work section in report, pilot evaluation protocol |
| Florian | Traffic rule engine & RC car integration -> rule logic (yield, stop, right of way), car-to-car collision detection, feedback triggers | Concept, implementation section in report, demo preparation |
| Serhat | Scene understanding & UI -> carpet road detection, visual AR feedback design (hazard highlights, rule labels), user-facing interaction | Concept, reflection section in report, demo preparation |

---

## 5. Planned Prototype

The prototype runs on a Meta Quest headset. The carpet roads are detected and spatially anchored at startup using marker-based or plane-detection methods (needs more research in how we do this). An toy car fitted with a visible marker is tracked in real time. The traffic rule engine runs on-device or via a connected laptop and governs the behaviour of 2-3 virtual cars that follow the carpet roads autonomously using basic pathfinding.

When the physical car violates a rule, runs a red light, fails to yield, enters a one-way road the wrong way. The virtual car in that lane reacts visibly. The feedback is designed to be readable without any explanation: a car braking hard, flashing hazard lights, or a near-miss animation (if possible in scope).

---

## 6. Pilot Evaluation

Instead of a formal user study, we will run a self-directed pilot (also maybe with others from the course or study): each participant interacts with the prototype independently and documents their experience. They should look at three things: does the spatial anchoring hold under movement, are the AR feedback events readable and timely, and does breaking a rule feel meaningfully different from following one. We compile these observations into a reflection in the final report, which forms the methodological engagement component of the project