####################################
## Author: Kyle Tanous            ##
## Email:  ktanous@vt.edu         ##
##                                ##
## Metric collection from         ##
## bridge inspection AR testbed   ##
##                                ##
## Usage: place in same directory ##
## as data files and run as:      ##
## 'python DefectAnalysis.py'     ##
##                                ##
####################################

import glob, re

class Defect(object):
	def __init__(self, name, surf, px, py, pz, marked, real, n_neighbor):
		self.name = name
		self.surf = surf
		self.position = [float(px), float(py), float(pz)]
		self.marked = (marked == 'False')
		self.real = (real == 'False')
		self.nearest = n_neighbor.split('\\')[0]
		self.selected = 0

class Selection(object):
	def __init__(self, time, action, px, py, pz, defect, angle):
		self.time = float(time)
		self.action = action
		self.position = [float(px), float(py), float(pz)]
		self.defect = defect
		self.angle = float(angle)

	def __eq__(self, other):
		x_eq = self.position[0] == other.position[0]
		y_eq = self.position[1] == other.position[1]
		z_eq = self.position[2] == other.position[2]
		return x_eq and y_eq and z_eq



SEL_THRESH = 0.0
participant_runs = []

# Collect all relevant data files in current directory
for fname in glob.glob('./*p[0-9]*r[0-9]*_*.txt'):
	pr = fname.split('_')[0]
	if pr[2:] not in participant_runs:
		participant_runs.append(pr[2:])

# Run analysis on all data sets
for pr in participant_runs:
	print('Generating results for %s...' % pr)

	defect_file = open('./%s_DefectData.txt' % pr, 'r')
	log_file = open('./%s_Log.txt' % pr, 'r')
	selection_file = open('./%s_Selections.txt' % pr, 'r')
	results_file = open('./%s_Results.txt' % pr, 'w')

	# Skip headers
	defect_file.readline()
	defect_file.readline()
	selection_file.readline()

	# Parse data files
	num_defects = 0
	defects = {}
	for defect in defect_file:
		vals = re.split(',|:', defect)
		if vals[0][:6] == 'Defect':
			defects[vals[0]] = Defect(vals[0], vals[1], vals[2][1:], vals[3][1:], 
				                      vals[4][1:-1], vals[5], vals[6], vals[7])
			num_defects += 1

	selections = []
	for selection in selection_file:
		vals = re.split(',|:', selection)
		selections.append(Selection(vals[0], vals[1], vals[2][1:], vals[3][1:], 
			                    vals[4][1:-1], vals[5], vals[6]))

	# Collect metrics for user's selections
	hits = []
	false_alarms = []
	for sel in selections:
		sel_pos = sel.position
		def_pos = defects[sel.defect].position
		dist = sum((d - s) ** 2 for d, s in zip(def_pos, sel_pos)) ** 0.5
		if dist <= SEL_THRESH:
			if sel.action == 'Selection':
				defects[sel.defect].selected += 1
				hits.append(sel)
			elif sel.action == 'Deselection':
				defects[sel.defect].selected -= 1
				hits.remove(sel)
		else:
			if sel.action == 'Selection':
				false_alarms.append(sel)
			elif sel.action == 'Deselection':
				false_alarms.remove(sel)

	# Print summary
	misses = num_defects - sum(min(defects[k].selected, 1) for k in defects)
	print('#################################')
	print('############ SUMMARY ############')
	print('## Total Targets: %d' % num_defects)
	print('## Hits: %d' % len(hits))
	print('## False Alarms: %d' % len(false_alarms))
	print('## Misses: %d' % misses)
	print('##')
	print('## Detailed results found in: %s_Results.txt' % pr)
	print('#################################')

	# Print detailed results to file
	results_file.write('#################################\n')
	results_file.write('############ SUMMARY ############\n')
	results_file.write('## Total Targets: %d\n' % num_defects)
	results_file.write('## Hits: %d\n' % len(hits))
	results_file.write('## False Alarms: %d\n' % len(false_alarms))
	results_file.write('## Misses: %d\n' % misses)
	results_file.write('#################################\n')
	results_file.write('\n######### HITS #########\n')
	results_file.write('<Time>:<Action>:<Location>:<Nearest Defect>:<Viewing Angle to Defect>\n')
	for h in hits:
		results_file.write('%f:%s:%s:%s:%f\n' % (h.time, h.action, str(h.position), h.defect, h.angle))
	results_file.write('\n##### FALSE ALARMS #####\n')
	results_file.write('<Time>:<Action>:<Location>:<Nearest Defect>:<Viewing Angle to Defect>\n')
	for f in false_alarms:
		results_file.write('%f:%s:%s:%s:%f\n' % (f.time, f.action, str(f.position), f.defect, f.angle))
	results_file.write('\n##### TARGET HITS ######\n')
	results_file.write('<Name>:<Surface>:<Location>:<Marked>:<Real>:<Nearest Defect>\n')
	for k in defects:
		d = defects[k]
		if d.selected > 0:
			results_file.write('%s:%s:%s:%s:%s:%s' % (d.name, d.surf, str(d.position), str(d.marked), str(d.real), d.nearest))
	results_file.write('\n##### TARGET MISSES ####\n')
	results_file.write('<Name>:<Surface>:<Location>:<Marked>:<Real>:<Nearest Defect>\n')
	for k in defects:
		d = defects[k]
		if d.selected == 0:
			results_file.write('%s:%s:%s:%s:%s:%s' % (d.name, d.surf, str(d.position), str(d.marked), str(d.real), d.nearest))

	defect_file.close()
	log_file.close()
	selection_file.close()
	results_file.close()

